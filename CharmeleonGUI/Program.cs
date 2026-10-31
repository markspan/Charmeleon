namespace Charmeleon
{
    /// <summary>
    /// Application entry point and amplifier selection: try EEGO first, then TMSi,
    /// then fall back to demo mode (reporting to the user why, if nothing opened).
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Selects a driver (EEGO, then TMSi) or demo mode, then runs the main window.
        /// </summary>
        /// <param name="args">See <see cref="ParseArguments"/>.</param>
        [STAThread]
        static void Main(string[] args)
        {
            ErrorHandler.Register();

            var options = ParseArguments(args);

            ApplicationConfiguration.Initialize();

            IImpedanceDriver? driver = null;
            bool demo = false;
            bool eegoFound = false;
            string? eegoError = null;
            string? tmsiError = null;
            bool tmsiLoadError = false;

            // The amplifier the caller (the acquisition server's driver) uses, if it said: only that kind is
            // tried, so that Charmeleon never takes another amplifier connected to the same PC
            bool tryEego = options.Amplifier is null || options.Amplifier.Equals("eego", StringComparison.OrdinalIgnoreCase);
            bool tryTmsi = options.Amplifier is null || options.Amplifier.Equals("tmsi", StringComparison.OrdinalIgnoreCase);

            // 1. EEGO: an empty amplifier list means "none connected" (not an error);
            //    an exception means the SDK or amplifier failed.
            if (tryEego)
            {
                try
                {
                    var amps = EegoDriver.GetAmplifiers();
                    if (amps.Length > 0)
                    {
                        eegoFound = true;
                        var amp = options.Device == null ? amps[0] : amps.FirstOrDefault(a => a.Serial == options.Device);
                        if (amp.Serial == null && options.Device != null)
                            eegoError = $"no amplifier with serial number {options.Device} (found {string.Join(", ", amps.Select(a => a.Serial))}).";
                        else
                            driver = new EegoDriver(amp.Id);
                    }
                }
                catch (Exception ex)
                {
                    eegoError = Describe(ex);
                }
            }

            // 2. TMSi: the wrapper throws both for "no device" and for real failures
            //    (e.g. TMSiSDK.dll missing, or the driver assembly failing to load).
            if (driver == null && tryTmsi)
            {
                try { driver = new TmsiDriver(TmsiDriver.ConnectionType(options.Protocol), options.Device); }
                catch (Exception ex)
                {
                    tmsiError = Describe(ex);
                    tmsiLoadError = IsLoadError(ex);
                }
            }

            // 3. Nothing opened -> demo mode, but tell the user why.
            if (driver == null)
            {
                demo = true;

                string eegoLine = eegoError != null ? $"EEGO: {eegoError}"
                                : eegoFound ? "EEGO: an amplifier was detected but could not be opened."
                                            : "EEGO: no amplifier found.";
                string tmsiLine = tmsiError != null ? $"TMSi: {tmsiError}" : "TMSi: no amplifier found.";
                string msg = tryEego && tryTmsi
                    ? "No amplifier could be opened, so Charmeleon is starting in demo mode.\n\n" + eegoLine + "\n" + tmsiLine
                    : $"The acquisition server uses {(tryEego ? "an eego" : "a TMSi")} amplifier, which Charmeleon could not open, " +
                      "so it is starting in demo mode.\n\n" + (tryEego ? eegoLine : tmsiLine);

                // A DLL/load failure (or an EEGO exception) is a real problem worth
                // flagging; a plain "not found" is just informational.
                bool serious = eegoError != null || tmsiLoadError;
                MessageBox.Show(msg,
                    "Charmeleon - No amplifier connected",
                    MessageBoxButtons.OK,
                    serious ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }

            Application.Run(new MainGUI(driver, demo, options));

            driver?.Dispose();
            EegoDriver.SdkExit();
        }

        /// <summary>
        /// The command line: <c>Charmeleon.exe [montage.json] [--save-on-close] [--result result.json]
        /// [--amplifier eego|tmsi] [--device name] [--protocol USB|WiFi|Network|Bluetooth]</c>.
        /// A .json file opens as the montage; <c>--save-on-close</c> saves changes to it without asking when
        /// the window closes; <c>--result</c> names the file Charmeleon writes on closing, see
        /// <see cref="MainGUI.WriteResult"/>; the others choose the amplifier, see <see cref="StartOptions"/>.
        /// The OpenViBE UG acquisition server passes them all.
        /// </summary>
        internal static StartOptions ParseArguments(IReadOnlyList<string> args)
        {
            string? montage = null, result = null, amplifier = null, device = null, protocol = null;
            bool saveOnClose = false;
            for (int i = 0; i < args.Count; i++)
            {
                bool hasValue = i + 1 < args.Count;
                if (string.Equals(args[i], "--save-on-close", StringComparison.OrdinalIgnoreCase)) saveOnClose = true;
                else if (string.Equals(args[i], "--result", StringComparison.OrdinalIgnoreCase) && hasValue) result = args[++i];
                else if (string.Equals(args[i], "--amplifier", StringComparison.OrdinalIgnoreCase) && hasValue) amplifier = args[++i];
                else if (string.Equals(args[i], "--device", StringComparison.OrdinalIgnoreCase) && hasValue) device = args[++i];
                else if (string.Equals(args[i], "--protocol", StringComparison.OrdinalIgnoreCase) && hasValue) protocol = args[++i];
                else if (montage == null && args[i].EndsWith(".json", StringComparison.OrdinalIgnoreCase)) montage = args[i];
            }
            return new StartOptions(montage, saveOnClose, result, amplifier, device, protocol);
        }

        /// <summary>Human-readable message including the innermost exception cause.</summary>
        static string Describe(Exception ex)
        {
            var root = ex;
            while (root.InnerException != null) root = root.InnerException;
            return ReferenceEquals(root, ex)
                ? ex.Message
                : $"{ex.Message} ({root.GetType().Name}: {root.Message})";
        }

        /// <summary>True if the exception chain indicates a DLL / assembly load failure.</summary>
        static bool IsLoadError(Exception ex)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
                if (e is BadImageFormatException or FileLoadException or FileNotFoundException
                       or DllNotFoundException or TypeLoadException or TypeInitializationException)
                    return true;
            return false;
        }
    }
}

