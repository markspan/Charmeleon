using System.Runtime.InteropServices;

namespace Charmeleon
{
    /// <summary>Identifies a connected EEGO amplifier.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct AmplifierInfo
    {
        /// <summary>Amplifier identifier used by the SDK.</summary>
        public int Id;

        /// <summary>Device serial number string.</summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string Serial;
    }

    /// <summary>Describes a single channel on an EEGO amplifier.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ChannelInfo
    {
        /// <summary>Zero-based channel index.</summary>
        public int Index;

        /// <summary>
        /// Channel type: 0=reference, 1=bipolar, 2=accel, 3=gyro, 4=magneto,
        /// 5=trigger, 6=sample_counter, 7=impedance_reference, 8=impedance_ground.
        /// </summary>
        public int Type;
    }

    /// <summary>
    /// <see cref="IImpedanceDriver"/> implementation for eemagine EEGO amplifiers,
    /// via P/Invoke to eego-SDK.dll.
    /// </summary>
    public sealed class EegoDriver : IImpedanceDriver
    {
        const string DLL = "eego-SDK";

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        static extern void eemagine_sdk_init();
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        static extern void eemagine_sdk_exit();
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        static extern int eemagine_sdk_get_amplifiers_info([Out] AmplifierInfo[] info, int size);
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        static extern int eemagine_sdk_open_amplifier(int id);
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        static extern int eemagine_sdk_close_amplifier(int id);
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        static extern int eemagine_sdk_get_amplifier_channel_list(int id, [Out] ChannelInfo[] channels, int size);
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        static extern int eemagine_sdk_open_impedance_stream(int ampId, ChannelInfo[] channels, int size);
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        static extern int eemagine_sdk_close_stream(int streamId);
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        static extern int eemagine_sdk_get_stream_channel_count(int streamId);
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        static extern int eemagine_sdk_prefetch(int streamId);
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        static extern int eemagine_sdk_get_data(int streamId, [Out] double[] buffer, int sizeInBytes);

        // Static flag: only one SDK initialisation per process is needed regardless of
        // how many EegoDriver instances are created (there is normally only one).
        static bool _sdkInitialized = false;

        /// <summary>Initialises the EEGO SDK if it has not been initialised yet.</summary>
        public static void SdkInit()
        {
            if (!_sdkInitialized)
            {
                eemagine_sdk_init();
                _sdkInitialized = true;
            }
        }

        /// <summary>Shuts down the EEGO SDK.</summary>
        public static void SdkExit()
        {
            if (_sdkInitialized)
            {
                eemagine_sdk_exit();
                _sdkInitialized = false;
            }
        }

        /// <summary>Returns information about all connected EEGO amplifiers.</summary>
        public static AmplifierInfo[] GetAmplifiers()
        {
            SdkInit();
            var buf = new AmplifierInfo[16];
            int count = eemagine_sdk_get_amplifiers_info(buf, buf.Length);
            if (count <= 0) return Array.Empty<AmplifierInfo>();
            Array.Resize(ref buf, count);
            return buf;
        }

        int _ampId = -1;
        int _streamId = -1;
        int _channelCount = 0;
        double[] _lastValues = Array.Empty<double>();

        /// <inheritdoc/>
        public int ChannelCount => _channelCount;

        /// <summary>
        /// Opens the specified amplifier and starts an impedance stream on its reference channels.
        /// </summary>
        /// <param name="amplifierId">Amplifier ID returned by <see cref="GetAmplifiers"/>.</param>
        public EegoDriver(int amplifierId)
        {
            SdkInit();
            _ampId = amplifierId;

            int result = eemagine_sdk_open_amplifier(_ampId);
            if (result < 0)
                throw new Exception($"Cannot open amplifier {amplifierId} (error {result}).");

            // Fetch full channel list, keep only reference channels (type 0)
            var allChannels = new ChannelInfo[128];
            int total = eemagine_sdk_get_amplifier_channel_list(_ampId, allChannels, allChannels.Length);
            if (total <= 0)
                throw new Exception("Cannot read channel list from amplifier.");

            var refChannels = allChannels.Take(total).Where(c => c.Type == 0).ToArray();
            if (refChannels.Length == 0)
                throw new Exception("No reference channels found on amplifier.");

            _streamId = eemagine_sdk_open_impedance_stream(_ampId, refChannels, refChannels.Length);
            if (_streamId < 0)
                throw new Exception($"Cannot open impedance stream (error {_streamId}).");

            _channelCount = eemagine_sdk_get_stream_channel_count(_streamId);
            _lastValues = new double[_channelCount];
        }

        /// <summary>
        /// Returns the most recent impedance sample per channel, in Ohm.
        /// Returns the previous values if no new data has arrived yet.
        /// </summary>
        public double[] GetImpedances()
        {
            int bytes = eemagine_sdk_prefetch(_streamId);
            if (bytes <= 0) return _lastValues;

            var buf = new double[bytes / sizeof(double)];
            int written = eemagine_sdk_get_data(_streamId, buf, bytes);
            if (written <= 0) return _lastValues;

            int doubles = written / sizeof(double);
            int samples = doubles / _channelCount;
            if (samples <= 0) return _lastValues;

            // Take the last complete sample
            int lastOffset = (samples - 1) * _channelCount;
            for (int ch = 0; ch < _channelCount && lastOffset + ch < buf.Length; ch++)
                _lastValues[ch] = buf[lastOffset + ch];

            return _lastValues;
        }

        /// <inheritdoc/>
        public double[] GetImpedancesKOhm()
        {
            var raw = GetImpedances();
            var result = new double[raw.Length];
            for (int i = 0; i < raw.Length; i++)
                result[i] = raw[i] / 1000.0;
            return result;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_streamId >= 0) { eemagine_sdk_close_stream(_streamId); _streamId = -1; }
            if (_ampId >= 0) { eemagine_sdk_close_amplifier(_ampId); _ampId = -1; }
        }
    }
}
