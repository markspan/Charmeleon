using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Charmeleon
{
    /// <summary>
    /// The web server behind Web > Web View: serves the live impedance head map over
    /// HTTPS on port 8765 to any browser on the local network.
    ///
    /// HTTPS, with the self-signed <see cref="WebCertificate"/>, makes the page a
    /// secure context, which the Screen Wake Lock on phones and tablets requires.
    /// There is no admin requirement, no certificate store and no netsh binding:
    /// the server is a plain TCP listener with TLS on top.
    ///
    /// Connections are served asynchronously, so a browser that stalls costs no
    /// thread and holds up no one else, and every stage has a time limit. Each
    /// connection carries one request (<c>Connection: close</c>) and is then shut
    /// down cleanly. Every way a connection can end goes to <see cref="WebLog"/>,
    /// so a device that cannot connect always leaves its reason behind.
    ///
    /// Routes: <c>/</c> and <c>/app.js</c> (the page), <c>/colormap</c> (the colour
    /// table as JSON) and <c>/stream</c> (live impedances as server-sent events,
    /// once per second). Plain HTTP on the same port is redirected to HTTPS.
    /// </summary>
    sealed class WebServer : IDisposable
    {
        /// <summary>TCP port of the Web View; the installer opens it in the firewall.</summary>
        public const int Port = 8765;

        /// <summary>Longest a browser may take to start talking and to finish the TLS handshake.</summary>
        const int HandshakeSeconds = 10;

        /// <summary>
        /// Longest a connection may wait for its request. Browsers open connections
        /// in advance, and Chrome keeps an unused one for up to 60 seconds, so this
        /// must be longer: closing first would race the browser's request.
        /// </summary>
        const int RequestSeconds = 90;

        /// <summary>Longest a browser may take to accept a response or a live update.</summary>
        const int WriteSeconds = 15;

        /// <summary>Longest to wait for the browser to close its end after a response.</summary>
        const int LingerSeconds = 2;

        /// <summary>Largest request head (request line plus headers) accepted.</summary>
        const int MaxHeadBytes = 8192;

        /// <summary>Interval between live updates on <c>/stream</c>.</summary>
        const int StreamIntervalMs = 1000;

        /// <summary>First byte of every TLS connection (a handshake record); anything else is plain HTTP.</summary>
        const byte TlsHandshakeRecord = 0x16;

        /// <summary>The page's static files by path, read once from the embedded resources.</summary>
        static readonly Dictionary<string, (string ContentType, byte[] Body)> _files = LoadFiles();

        readonly TcpListener _listener = new(IPAddress.Any, Port);

        // Cancelled by Dispose. Never disposed itself: connections that are still
        // finishing may link new deadlines to it.
        readonly CancellationTokenSource _stopping = new();

        readonly WebCertificate _certificate = null!;   // set whenever Started is true

        /// <summary>True when the server is listening; otherwise see <see cref="ErrorMessage"/>.</summary>
        public bool Started { get; }

        /// <summary>Why the server could not start (port in use, certificate failure), or null.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Folder for the Web View's certificate and connection log: <c>%LOCALAPPDATA%\Charmeleon</c>.</summary>
        public static string DataFolder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Charmeleon");

        /// <summary>
        /// Starts listening on <see cref="Port"/>. Never throws: a failure is reported
        /// through <see cref="Started"/> and <see cref="ErrorMessage"/>.
        /// </summary>
        public WebServer()
        {
            try
            {
                _certificate = new WebCertificate();
                _listener.Start();
            }
            catch (Exception ex)
            {
                // Whatever went wrong (port taken, key store refusing the key), it
                // must disable the Web View, not stop Charmeleon from starting.
                ErrorMessage = ex.Message;
                WebLog.Write($"Server could not start: {WebLog.Describe(ex)}");
                return;
            }

            Started = true;
            string? version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            WebLog.Write($"Server listening on port {Port} (Charmeleon {version})");

            // On the thread pool, away from the UI thread's synchronisation context,
            // so no part of the server ever runs on, or waits for, the UI thread.
            _ = Task.Run(AcceptLoopAsync);
        }

        /// <summary>The Web View URL on <paramref name="host"/> (an IP address or host name).</summary>
        public static string UrlFor(string host) => $"https://{host}:{Port}/";

        /// <summary>Stops accepting connections and ends the open ones, live streams included.</summary>
        public void Dispose()
        {
            if (_stopping.IsCancellationRequested) return;
            _stopping.Cancel();
            _listener.Stop();
            if (Started) WebLog.Write("Server stopped");
        }

        // ------------------------------------------------------------------ //
        //  Connections
        // ------------------------------------------------------------------ //

        /// <summary>Accepts connections until the server stops, serving each one concurrently.</summary>
        async Task AcceptLoopAsync()
        {
            while (!_stopping.IsCancellationRequested)
            {
                Socket socket;
                try
                {
                    socket = await _listener.AcceptSocketAsync(_stopping.Token);
                }
                catch (Exception) when (_stopping.IsCancellationRequested)
                {
                    break;   // Dispose stopped the listener
                }
                catch (SocketException ex)
                {
                    // A queued connection that was reset before it could be accepted;
                    // the others are unaffected.
                    WebLog.Write($"Accepting a connection failed: {WebLog.Describe(ex)}");
                    continue;
                }
                catch (Exception ex)
                {
                    WebLog.Write($"Server stopped accepting connections: {WebLog.Describe(ex)}");
                    break;
                }
                _ = ServeConnectionAsync(socket);
            }
        }

        /// <summary>
        /// Serves one connection from its first byte to its close. Never throws: every
        /// way it can end is logged against the browser's address and port, because
        /// nothing else observes this task.
        /// </summary>
        async Task ServeConnectionAsync(Socket socket)
        {
            string peer = socket.RemoteEndPoint?.ToString() ?? "?";
            void Log(string message) => WebLog.Write($"{peer,-21}  {message}");

            string stage = "waiting for the browser to start";
            try
            {
                using var network = new NetworkStream(socket, ownsSocket: true);
                var local = ((IPEndPoint)socket.LocalEndPoint!).Address;

                byte? first;
                using (var deadline = Deadline(HandshakeSeconds))
                    first = await PeekFirstByteAsync(socket, deadline.Token);
                if (first is null)
                {
                    Log("closed without sending anything");
                    return;
                }
                if (first != TlsHandshakeRecord)
                {
                    stage = "redirecting plain HTTP to HTTPS";
                    Log(await RedirectToHttpsAsync(network, local));
                    await CloseAsync(null, socket);
                    return;
                }

                stage = "in the TLS handshake";
                await using var tls = new SslStream(network, leaveInnerStreamOpen: false);
                using (var deadline = Deadline(HandshakeSeconds))
                    await tls.AuthenticateAsServerAsync(TlsOptions(local), deadline.Token);
                string protocol = Name(tls.SslProtocol);

                stage = $"waiting for the request ({protocol})";
                string? head;
                using (var deadline = Deadline(RequestSeconds))
                    head = await ReadHeadAsync(tls, deadline.Token);
                if (head is null)
                {
                    Log($"{protocol}, closed by the browser before sending a request (normal for a connection opened in advance)");
                    return;
                }

                stage = $"answering ({protocol})";
                Log($"{protocol}, {await RespondAsync(tls, head)}");
                await CloseAsync(tls, socket);
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                // Charmeleon is closing: not worth a line per connection.
            }
            catch (OperationCanceledException)
            {
                Log($"timed out while {stage}");
            }
            catch (AuthenticationException ex)
            {
                Log($"TLS handshake failed: {WebLog.Describe(ex)}");
            }
            catch (Exception ex)
            {
                // Usually the connection was lost (IOException, SocketException).
                Log($"failed while {stage}: {WebLog.Describe(ex)}");
            }
        }

        /// <summary>TLS settings for a connection that arrived on the local address <paramref name="local"/>.</summary>
        SslServerAuthenticationOptions TlsOptions(IPAddress local) => new()
        {
            ServerCertificateContext = _certificate.For(local),
            // None lets Windows pick the version (TLS 1.3 where it supports it well,
            // TLS 1.2 elsewhere), as Microsoft recommends, instead of forcing one.
            EnabledSslProtocols = SslProtocols.None,
            ClientCertificateRequired = false,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            AllowRenegotiation = false,
        };

        /// <summary>The connection's first byte, left unread; null if the browser closed instead.</summary>
        static async Task<byte?> PeekFirstByteAsync(Socket socket, CancellationToken token)
        {
            var buffer = new byte[1];
            int received = await socket.ReceiveAsync(buffer, SocketFlags.Peek, token);
            return received == 0 ? null : buffer[0];
        }

        /// <summary>
        /// Ends a served connection cleanly: a TLS close_notify (when on TLS), a TCP
        /// FIN, then reading and discarding whatever the browser still sends until it
        /// closes its end, for at most <see cref="LingerSeconds"/>. Closing a socket
        /// with unread input makes the OS send a reset instead, and a reset can make
        /// the browser discard a response it has not finished reading. Failures are
        /// ignored: by now the response is out, or the browser has already gone.
        /// </summary>
        static async Task CloseAsync(SslStream? tls, Socket socket)
        {
            try
            {
                using var linger = new CancellationTokenSource(TimeSpan.FromSeconds(LingerSeconds));
                if (tls != null) await tls.ShutdownAsync().WaitAsync(linger.Token);
                socket.Shutdown(SocketShutdown.Send);

                var discard = new byte[1024];
                while (await socket.ReceiveAsync(discard, SocketFlags.None, linger.Token) > 0)
                {
                    // Discard: only the browser's own close matters here.
                }
            }
            catch (Exception)
            {
                // See above: nothing left to save or report. This includes the
                // InvalidOperationException a TLS stream throws when a timed-out live
                // update left it unusable.
            }
        }

        /// <summary>A token that fires after <paramref name="seconds"/>, or at once when the server stops.</summary>
        CancellationTokenSource Deadline(int seconds)
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
            source.CancelAfter(TimeSpan.FromSeconds(seconds));
            return source;
        }

        // ------------------------------------------------------------------ //
        //  HTTP
        // ------------------------------------------------------------------ //

        /// <summary>Answers one request and returns a summary for the log, such as <c>GET /app.js 200</c>.</summary>
        async Task<string> RespondAsync(Stream stream, string head)
        {
            if (!HttpRequest.TryParse(head, out var request))
            {
                await WriteResponseAsync(stream, 400, "text/plain; charset=utf-8", "Bad request"u8.ToArray());
                return $"unreadable request \"{head.Split("\r\n")[0]}\" 400";
            }
            if (request.Method != "GET")
            {
                await WriteResponseAsync(stream, 405, "text/plain; charset=utf-8", "Only GET is supported"u8.ToArray(), "Allow: GET\r\n");
                return $"{request.Method} {request.Path} 405";
            }
            if (request.Path == "/stream")
                return $"GET /stream: {await StreamAsync(stream)}";

            int status = 200;
            if (request.Path == "/colormap")
            {
                await WriteResponseAsync(stream, status, "application/json", Encoding.UTF8.GetBytes(ImpedanceSource.GetColorMapJson()));
            }
            else if (_files.TryGetValue(request.Path, out var file))
            {
                await WriteResponseAsync(stream, status, file.ContentType, file.Body);
            }
            else
            {
                status = 404;
                await WriteResponseAsync(stream, status, "text/plain; charset=utf-8", "Not found"u8.ToArray());
            }

            // The page load names the browser, which is what a connection problem
            // usually comes down to.
            string browser = request.Path == "/" ? $", {request.UserAgent}" : "";
            return $"GET {request.Path} {status}{browser}";
        }

        /// <summary>
        /// Sends live impedances as server-sent events, one snapshot per second, until
        /// the browser goes away or Charmeleon closes. Returns how long it ran and why
        /// it ended, for the log.
        /// </summary>
        async Task<string> StreamAsync(Stream stream)
        {
            const string head =
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: text/event-stream\r\n" +
                "Cache-Control: no-cache\r\n" +
                "X-Accel-Buffering: no\r\n" +
                "Connection: close\r\n\r\n";

            var clock = Stopwatch.StartNew();
            string ending;
            try
            {
                await WriteAsync(stream, Encoding.ASCII.GetBytes(head));
                while (true)
                {
                    await WriteAsync(stream, Encoding.UTF8.GetBytes($"data: {ImpedanceSource.GetJson()}\n\n"));
                    await Task.Delay(StreamIntervalMs, _stopping.Token);
                }
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                ending = "Charmeleon closed";
            }
            catch (OperationCanceledException)
            {
                ending = $"the browser stopped receiving for {WriteSeconds} s";
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                ending = "the browser went away";
            }
            return $"live updates for {clock.Elapsed:hh\\:mm\\:ss}, until {ending}";
        }

        /// <summary>
        /// Answers plain HTTP (someone typed <c>http://</c>, or left the scheme out)
        /// with a redirect to the same address over HTTPS, instead of failing a TLS
        /// handshake on it. Returns a summary for the log.
        /// </summary>
        async Task<string> RedirectToHttpsAsync(Stream stream, IPAddress local)
        {
            string? head;
            using (var deadline = Deadline(RequestSeconds))
                head = await ReadHeadAsync(stream, deadline.Token);
            if (head is null)
                return "plain HTTP, closed before sending a request";
            if (!HttpRequest.TryParse(head, out var request))
            {
                await WriteResponseAsync(stream, 400, "text/plain; charset=utf-8", "Bad request"u8.ToArray());
                return $"plain HTTP, unreadable request \"{head.Split("\r\n")[0]}\" 400";
            }

            // Keep the address the browser used, but never echo anything that is not
            // a plain host name into the response.
            string host = request.Host is { } given && IsHostName(given) ? given : local.ToString();
            if (!host.Contains(':')) host += $":{Port}";
            string location = $"https://{host}{request.Path}";

            await WriteResponseAsync(stream, 301, "text/plain; charset=utf-8",
                Encoding.ASCII.GetBytes($"The Web View is at {location}\n"), $"Location: {location}\r\n");
            return $"plain HTTP {request.Method} {request.Path}, redirected to {location}";
        }

        /// <summary>True when <paramref name="host"/> is a plain host or address, optionally with a port.</summary>
        static bool IsHostName(string host) =>
            host.Length is > 0 and <= 255 &&
            host.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or ':' or '[' or ']');

        /// <summary>
        /// Reads a request head, up to and excluding the blank line that ends it.
        /// Returns null when the browser closed without sending anything.
        /// </summary>
        /// <exception cref="InvalidDataException">The head is larger than <see cref="MaxHeadBytes"/> or was cut off.</exception>
        static async Task<string?> ReadHeadAsync(Stream stream, CancellationToken token)
        {
            var buffer = new byte[MaxHeadBytes];
            int length = 0;
            while (true)
            {
                if (length == buffer.Length)
                    throw new InvalidDataException($"request head larger than {MaxHeadBytes} bytes");

                int read = await stream.ReadAsync(buffer.AsMemory(length), token);
                if (read == 0)
                {
                    if (length == 0) return null;
                    throw new InvalidDataException("the browser closed in the middle of its request");
                }

                // Search from 3 bytes back: the terminator may straddle two reads.
                int from = Math.Max(0, length - 3);
                length += read;
                int end = buffer.AsSpan(from, length - from).IndexOf("\r\n\r\n"u8);
                if (end >= 0) return Encoding.ASCII.GetString(buffer, 0, from + end);
            }
        }

        /// <summary>Writes a complete response with a known body, in a single write.</summary>
        async Task WriteResponseAsync(Stream stream, int status, string contentType, byte[] body, string extraHeaders = "")
        {
            string head =
                $"HTTP/1.1 {status} {ReasonPhrase(status)}\r\n" +
                $"Content-Type: {contentType}\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                extraHeaders +
                "Cache-Control: no-cache\r\n" +
                "Connection: close\r\n\r\n";
            await WriteAsync(stream, [.. Encoding.ASCII.GetBytes(head), .. body]);
        }

        /// <summary>Writes and flushes, giving up after <see cref="WriteSeconds"/>.</summary>
        async Task WriteAsync(Stream stream, byte[] bytes)
        {
            using var deadline = Deadline(WriteSeconds);
            await stream.WriteAsync(bytes, deadline.Token);
            await stream.FlushAsync(deadline.Token);
        }

        /// <summary>The standard reason phrase for the status codes this server sends.</summary>
        static string ReasonPhrase(int status) => status switch
        {
            200 => "OK",
            301 => "Moved Permanently",
            400 => "Bad Request",
            404 => "Not Found",
            405 => "Method Not Allowed",
            _ => "",
        };

        /// <summary>A TLS version as people write it, such as "TLS 1.3".</summary>
        static string Name(SslProtocols protocol) => protocol switch
        {
            SslProtocols.Tls13 => "TLS 1.3",
            SslProtocols.Tls12 => "TLS 1.2",
            _ => protocol.ToString(),
        };

        /// <summary>
        /// Reads the page's files from the embedded resources. A file missing from
        /// the build is left out (its path then answers 404) rather than failing the
        /// whole server.
        /// </summary>
        static Dictionary<string, (string ContentType, byte[] Body)> LoadFiles()
        {
            (string Path, string Resource, string ContentType)[] page =
            [
                ("/", "index.html", "text/html; charset=utf-8"),
                ("/app.js", "app.js", "text/javascript; charset=utf-8"),
            ];

            var files = new Dictionary<string, (string ContentType, byte[] Body)>();
            foreach (var (path, resource, contentType) in page)
            {
                using Stream? source = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Charmeleon.{resource}");
                if (source is null) continue;
                using var copy = new MemoryStream();
                source.CopyTo(copy);
                files[path] = (contentType, copy.ToArray());
            }
            return files;
        }

        /// <summary>The parts of an HTTP request this server uses.</summary>
        readonly record struct HttpRequest(string Method, string Path, string? Host, string? UserAgent)
        {
            /// <summary>Parses a request head: <c>GET /path?query HTTP/1.1</c> followed by headers.</summary>
            public static bool TryParse(string head, out HttpRequest request)
            {
                request = default;
                string[] lines = head.Split("\r\n");
                string[] start = lines[0].Split(' ');
                if (start.Length != 3 || !start[1].StartsWith('/') || !start[2].StartsWith("HTTP/1.", StringComparison.Ordinal))
                    return false;

                string? Header(string name) => lines
                    .Skip(1)
                    .Select(line => line.Split(':', 2))
                    .Where(pair => pair.Length == 2 && pair[0].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                    .Select(pair => pair[1].Trim())
                    .FirstOrDefault();

                string path = start[1].Split('?', 2)[0];
                request = new HttpRequest(start[0], path, Header("Host"), Header("User-Agent"));
                return true;
            }
        }
    }
}
