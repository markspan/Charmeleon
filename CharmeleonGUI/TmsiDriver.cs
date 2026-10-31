using System.Runtime.InteropServices;

namespace Charmeleon
{
    /// <summary>
    /// <see cref="IImpedanceDriver"/> implementation for TMSi Refa amplifiers.
    /// Pure C# P/Invoke into TMSiSDK.dll (installed in System32 by the TMSi
    /// device driver); no C++/CLI wrapper required.
    /// </summary>
    public sealed class TmsiDriver : IImpedanceDriver
    {
        // --- TMSiSDK constants ---
        const int TMSiConnectionUSB = 3;     // _TMSiConnectionEnum: USB 2.0 direct
        const uint MEASURE_MODE_IMPEDANCE_EX = 0x3;
        const int IC_OHM_200 = 6;     // 200 kOhm impedance limit
        const int MaxFrontEndNameLength = 256;

        IntPtr _handle = IntPtr.Zero;
        IntPtr _deviceList = IntPtr.Zero;
        int _deviceCount;
        int _channelCount;
        uint[] _buffer = [];
        uint _bufferBytes;
        double[] _lastValues = [];

        // SIGNAL_FORMAT (TMSiSDK.h): one entry per channel, Size bytes each; Elements at offset 4, Type at offset 8.
        // Size, Elements, Type, SubType, Format, Bytes, UnitGain, UnitOffSet, UnitId, UnitExponent (4 bytes each),
        // Name (40 WCHAR), Port (4), PortName (40 WCHAR), SerialNumber (4)
        const int SignalFormatSize = 10 * 4 + 80 + 4 + 80 + 4;
        const uint ChannelTypeExg = 1, ChannelTypeBip = 2, ChannelTypeAux = 3;

        /// <inheritdoc/>
        public AmplifierLayout Layout { get; } = AmplifierLayout.Demo;

        /// <inheritdoc/>
        /// <remarks>Every device channel, in device order: TMSi measures the impedance of each input.</remarks>
        public IReadOnlyList<ChannelAddress> ImpedanceChannels { get; } = [];

        /// <summary>The SDK's connection type for a protocol name as the OpenViBE driver has it; USB for none or an unknown one.</summary>
        public static int ConnectionType(string? protocol) => protocol?.ToUpperInvariant() switch
        {
            "BLUETOOTH" => 2,
            "WIFI" => 4,
            "NETWORK" => 5,
            _ => TMSiConnectionUSB,
        };

        /// <summary>
        /// Opens a TMSi device and starts impedance-data mode.
        /// Throws if TMSiSDK.dll is absent, the device is not found, or setup fails.
        /// </summary>
        /// <param name="connectionType">How to reach it, see <see cref="ConnectionType"/>.</param>
        /// <param name="device">The device's name as the SDK lists it, as the OpenViBE driver's Device setting has it; null for the first one.</param>
        public TmsiDriver(int connectionType = TMSiConnectionUSB, string? device = null)
        {
            int err = 0;
            _handle = LibraryInit(connectionType, ref err);
            if (_handle == IntPtr.Zero)
                throw new Exception("TMSi library could not be initialised. Is the Refa (blue box) switched on and connected?");

            _deviceList = GetDeviceList(_handle, ref _deviceCount);
            if (_deviceList == IntPtr.Zero || _deviceCount == 0)
                throw new Exception("No TMSi device found.");

            // DeviceList is an array of char* device locators; pass the chosen one straight to Open.
            IntPtr locator = Marshal.ReadIntPtr(_deviceList, 0);
            if (device != null)
            {
                var names = new List<string>();
                locator = IntPtr.Zero;
                for (int i = 0; i < _deviceCount; i++)
                {
                    IntPtr entry = Marshal.ReadIntPtr(_deviceList, i * IntPtr.Size);
                    string name = Marshal.PtrToStringAnsi(entry) ?? "";
                    names.Add(name);
                    if (name == device) locator = entry;
                }
                if (locator == IntPtr.Zero)
                    throw new Exception($"TMSi device {device} not found (found {string.Join(", ", names)}).");
            }
            if (!Open(_handle, locator))
                throw new Exception("Could not open the TMSi device.");

            // Signal format gives the channel count (SIGNAL_FORMAT.Elements at byte offset 4).
            var frontEndName = new byte[MaxFrontEndNameLength];
            IntPtr psf = GetSignalFormat(_handle, frontEndName);
            if (psf == IntPtr.Zero)
                throw new Exception("Could not read the TMSi signal format.");
            _channelCount = Marshal.ReadInt32(psf, 4);
            if (_channelCount <= 0)
            {
                Free(psf);
                throw new Exception("TMSi reported zero channels.");
            }

            // Number the EXG, BIP and AUX channels each in device order, as the OpenViBE driver does; a
            // montage's HardwareChannel n is the n-th EXG channel, wherever the device lists it
            int stride = Marshal.ReadInt32(psf, 0);
            if (stride < SignalFormatSize) stride = SignalFormatSize;
            var channels = new ChannelAddress[_channelCount];
            int exg = 0, bip = 0, aux = 0, other = 0;
            for (int i = 0; i < _channelCount; i++)
            {
                uint type = (uint)Marshal.ReadInt32(psf, i * stride + 8);
                channels[i] = type switch
                {
                    ChannelTypeExg => new ChannelAddress(ChannelKind.Eeg, ++exg),
                    ChannelTypeBip => new ChannelAddress(ChannelKind.Bipolar, ++bip),
                    ChannelTypeAux => new ChannelAddress(ChannelKind.Auxiliary, ++aux),
                    _ => new ChannelAddress(ChannelKind.Other, ++other),
                };
            }
            Free(psf);
            ImpedanceChannels = channels;
            string frontEnd = System.Text.Encoding.ASCII.GetString(frontEndName).Split('\0')[0].Trim();
            Layout = new AmplifierLayout(frontEnd.Length > 0 ? "TMSi " + frontEnd : "TMSi", exg, bip, aux);

            // Impedance mode samples at rate 0; allocate the sample buffer.
            uint sampleRateMilliHz = 0;
            uint bufferSamples = 1000;
            if (!SetSignalBuffer(_handle, ref sampleRateMilliHz, ref bufferSamples))
                throw new Exception("TMSi SetSignalBuffer failed.");

            _buffer = new uint[bufferSamples * (uint)_channelCount];
            _bufferBytes = (uint)_buffer.Length * sizeof(uint);

            if (!Start(_handle))
                throw new Exception("TMSi Start failed.");
            if (!SetMeasuringMode(_handle, MEASURE_MODE_IMPEDANCE_EX, IC_OHM_200))
                throw new Exception("TMSi could not enter impedance mode.");

            _lastValues = new double[_channelCount];
        }

        /// <inheritdoc/>
        public double[] GetImpedancesKOhm()
        {
            int bytesReturned = GetSamples(_handle, _buffer, _bufferBytes);
            if (bytesReturned <= 0) return _lastValues;

            // The first sample holds one impedance value per channel, already in
            // kOhm (0-255 scale).
            for (int ch = 0; ch < _channelCount && ch < _buffer.Length; ch++)
                _lastValues[ch] = _buffer[ch];

            return _lastValues;
        }

        /// <summary>Stops the device and releases the native library handle.</summary>
        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            try { Stop(_handle); } catch { }
            try { Close(_handle); } catch { }
            if (_deviceList != IntPtr.Zero)
                try { FreeDeviceList(_handle, _deviceCount, _deviceList); } catch { }
            try { LibraryExit(_handle); } catch { }
            _handle = IntPtr.Zero;
        }

        // ------------------------------------------------------------------ //
        //  TMSiSDK.dll P/Invoke (all __stdcall; BOOLEAN is one byte)
        // ------------------------------------------------------------------ //
        const string DLL = "TMSiSDK.dll";
        const CallingConvention CC = CallingConvention.StdCall;

        [DllImport(DLL, CallingConvention = CC)]
        static extern IntPtr LibraryInit(int connectionType, ref int errorCode);

        [DllImport(DLL, CallingConvention = CC)]
        static extern int LibraryExit(IntPtr handle);

        [DllImport(DLL, CallingConvention = CC)]
        static extern IntPtr GetDeviceList(IntPtr handle, ref int nrOfDevices);

        [DllImport(DLL, CallingConvention = CC)]
        static extern void FreeDeviceList(IntPtr handle, int nrOfDevices, IntPtr deviceList);

        [DllImport(DLL, CallingConvention = CC)]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool Open(IntPtr handle, IntPtr deviceLocator);

        [DllImport(DLL, CallingConvention = CC)]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool Close(IntPtr handle);

        [DllImport(DLL, CallingConvention = CC)]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool Start(IntPtr handle);

        [DllImport(DLL, CallingConvention = CC)]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool Stop(IntPtr handle);

        [DllImport(DLL, CallingConvention = CC)]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool SetSignalBuffer(IntPtr handle, ref uint sampleRate, ref uint bufferSize);

        [DllImport(DLL, CallingConvention = CC)]
        static extern int GetSamples(IntPtr handle, [Out] uint[] sampleBuffer, uint sizeInBytes);

        [DllImport(DLL, CallingConvention = CC)]
        static extern IntPtr GetSignalFormat(IntPtr handle, [In, Out] byte[] frontEndName);

        [DllImport(DLL, CallingConvention = CC)]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool Free(IntPtr memory);

        [DllImport(DLL, CallingConvention = CC)]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool SetMeasuringMode(IntPtr handle, uint mode, int value);
    }
}
