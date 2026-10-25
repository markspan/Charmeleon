using System.ComponentModel;
using System.Text;

namespace Charmeleon
{
    /// <summary>
    /// The Web View server's connection log, <c>%LOCALAPPDATA%\Charmeleon\web.log</c>.
    /// Every connection leaves a timestamped line saying how it ended: page served,
    /// live updates ended, plain HTTP redirected, or exactly why it failed (TLS
    /// handshake refused, timed out, connection lost). When a phone or tablet
    /// cannot open the Web View, this file holds the reason.
    ///
    /// The file stays small: once it passes <see cref="MaxBytes"/> it is renamed to
    /// <c>web.log.1</c> (replacing the previous one) and a new file starts.
    /// Writing is serialised by a lock and never throws, because a log that cannot
    /// be written must not take the server down with it.
    /// </summary>
    static class WebLog
    {
        /// <summary>Size at which the log is rotated to <c>web.log.1</c>.</summary>
        const long MaxBytes = 1024 * 1024;

        static readonly object _lock = new();

        // Plain UTF-8: Encoding.UTF8 would start each new file with a byte-order mark.
        static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false);

        /// <summary>Full path of the current log file.</summary>
        public static string FilePath { get; } = Path.Combine(WebServer.DataFolder, "web.log");

        /// <summary>Appends one line, prefixed with the local date and time.</summary>
        public static void Write(string message)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}";
            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory(WebServer.DataFolder);
                    var file = new FileInfo(FilePath);
                    if (file.Exists && file.Length > MaxBytes)
                        File.Move(FilePath, FilePath + ".1", overwrite: true);
                    File.AppendAllText(FilePath, line, _utf8);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Nowhere to report it; the server carries on without its log.
                }
            }
        }

        /// <summary>
        /// One-line description of an exception and everything that caused it, for
        /// example <c>AuthenticationException: Authentication failed ... | caused by
        /// Win32Exception (0x80090326): The message received was unexpected or badly
        /// formatted.</c> The Win32 code is the part that identifies a TLS failure on
        /// Windows.
        /// </summary>
        public static string Describe(Exception ex)
        {
            var parts = new List<string>();
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                string code = e is Win32Exception win32 ? $" (0x{win32.NativeErrorCode:X8})" : "";
                parts.Add($"{e.GetType().Name}{code}: {e.Message}");
            }
            return string.Join(" | caused by ", parts);
        }
    }
}
