namespace Charmeleon
{
    /// <summary>
    /// Application entry point and amplifier selection: try EEGO first, then TMSi,
    /// then fall back to demo mode (reporting to the user why, if nothing opened).
    /// </summary>
    internal static class Program
    {
        /// <summary>Selects a driver (EEGO, then TMSi) or demo mode, then runs the main window.</summary>
        [STAThread]
        static void Main()
        {
            ErrorHandler.Register();

            ApplicationConfiguration.Initialize();

            IImpedanceDriver? driver = null;
            bool demo = false;
            bool eegoFound = false;
            string? eegoError = null;
            string? tmsiError = null;
            bool tmsiLoadError = false;

            // 1. EEGO: an empty amplifier list means "none connected" (not an error);
            //    an exception means the SDK or amplifier failed.
            try
            {
                var amps = EegoDriver.GetAmplifiers();
                if (amps.Length > 0)
                {
                    eegoFound = true;
                    driver = new EegoDriver(amps[0].Id);
                }
            }
            catch (Exception ex)
            {
                eegoError = Describe(ex);
            }

            // 2. TMSi: the wrapper throws both for "no device" and for real failures
            //    (e.g. TMSiSDK.dll missing, or the driver assembly failing to load).
            if (driver == null)
            {
                try { driver = new TmsiDriver(); }
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

                string msg =
                    "No amplifier could be opened, so Charmeleon is starting in demo mode.\n\n" +
                    (eegoError != null ? $"EEGO: {eegoError}"
                                       : eegoFound ? "EEGO: an amplifier was detected but could not be opened."
                                                   : "EEGO: no amplifier found.") + "\n" +
                    (tmsiError != null ? $"TMSi: {tmsiError}"
                                       : "TMSi: no amplifier found.");

                // A DLL/load failure (or an EEGO exception) is a real problem worth
                // flagging; a plain "not found" is just informational.
                bool serious = eegoError != null || tmsiLoadError;
                MessageBox.Show(msg,
                    "Charmeleon - No amplifier connected",
                    MessageBoxButtons.OK,
                    serious ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }

            Application.Run(new MainGUI(driver, demo));

            driver?.Dispose();
            EegoDriver.SdkExit();
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

