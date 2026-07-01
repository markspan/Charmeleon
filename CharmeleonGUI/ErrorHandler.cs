using System.Reflection;

namespace Charmeleon
{
    /// <summary>
    /// Registers a global unhandled-exception handler that turns unhandled errors (including missing-DLL load failures) into clear dialogs instead of silent crashes.
    /// </summary>
    static class ErrorHandler
    {
        // Known DLL -> friendly description + fix hint
        static readonly (string Key, string Name, string Hint)[] _knownDlls =
        [
            ("eego",            "EEGO amplifier SDK (eego-SDK.dll)",
             "Copy eego-SDK.dll to the application folder.\nObtain it from the eemagine EEGO software installation."),

            ("TMSiSDK",         "TMSi amplifier SDK (TMSiSDK.dll)",
             "TMSiSDK.dll is not installed.\nInstall the TMSi device driver; it places TMSiSDK.dll in C:\\Windows\\System32."),
        ];

        /// <summary>
        /// Hooks <see cref="Application.ThreadException"/> and
        /// <see cref="AppDomain.UnhandledException"/> so that unhandled errors
        /// display a friendly dialog instead of crashing silently.
        /// </summary>
        public static void Register()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => Show(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                Show(e.ExceptionObject as Exception);
                // AppDomain.UnhandledException fires after the message loop has
                // already terminated, so Application.Exit() has no effect here.
                // Environment.Exit forces an immediate clean process exit.
                Environment.Exit(1);
            };
        }

        static void Show(Exception? ex)
        {
            // Unwrap wrapper exceptions to reach the root cause
            while (ex is TypeInitializationException or TargetInvocationException
                   && ex.InnerException is not null)
                ex = ex.InnerException;

            string message = ex switch
            {
                DllNotFoundException d => DllMessage(d),
                BadImageFormatException b => $"A DLL was found but could not be loaded " +
                                               $"(wrong CPU architecture).\n\n{b.FileName}\n\n" +
                                               "Make sure you are running the 64-bit version of Charmeleon.",
                FileNotFoundException f => $"A required file was not found.\n\n{f.FileName}\n\n" +
                                               "Make sure all application files are present.",
                null => "An unknown error occurred.",
                _ => $"{ex.GetType().Name}\n\n{ex.Message}"
            };

            MessageBox.Show(message, "Charmeleon - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        static string DllMessage(DllNotFoundException ex)
        {
            string raw = ex.Message.ToLowerInvariant();
            foreach (var (key, name, hint) in _knownDlls)
            {
                if (raw.Contains(key.ToLowerInvariant()))
                    return $"A required DLL could not be loaded.\n\n" +
                           $"Component: {name}\n\n{hint}";
            }
            return $"A required DLL could not be loaded.\n\n{ex.Message}\n\n" +
                   "Make sure all DLLs are present in the application folder.";
        }
    }
}
