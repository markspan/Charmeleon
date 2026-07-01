using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Charmeleon
{
    /// <summary>
    /// Lightweight built-in HTTPS server (port 8765) that serves the live
    /// impedance head map to any browser on the local network.
    ///
    /// It uses a self-signed certificate generated in-process (cached under
    /// %LOCALAPPDATA%\Charmeleon), so there is no admin requirement, no
    /// certificate store, and no netsh binding. Because the certificate is
    /// self-signed, browsers show a one-time "not trusted" warning that the
    /// user accepts once per device. HTTPS is what lets the page run as a
    /// secure context (so the Screen Wake Lock works on phones/tablets).
    ///
    /// Runs on a background thread; safe to instantiate once and keep alive
    /// for the application lifetime.
    /// </summary>
    sealed class WebServer : IDisposable
    {
        const int Port = 8765;

        readonly TcpListener _listener = new(IPAddress.Any, Port);
        readonly X509Certificate2 _cert = null!;
        readonly Thread _thread = null!;
        volatile bool _running = true;

        public bool Started { get; private set; }
        public string? ErrorMessage { get; private set; }

        public WebServer()
        {
            try
            {
                _cert = LoadOrCreateCertificate();
                _listener.Start();
                Started = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return;
            }

            _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "WebServer" };
            _thread.Start();
        }

        /// <summary>Returns the URL to display / encode as a QR code.</summary>
        public static string Url => $"https://{LocalIP()}:{Port}/";

        static string LocalIP()
        {
            try
            {
                using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                s.Connect("8.8.8.8", 65530);
                return ((IPEndPoint)s.LocalEndPoint!).Address.ToString();
            }
            catch { return "localhost"; }
        }

        /// <summary>URL for a specific host or IP.</summary>
        public static string UrlFor(string ip) => $"https://{ip}:{Port}/";

        /// <summary>
        /// All usable local IPv4 addresses, best guess first: the internet-routed
        /// address leads, then adapters that have a default gateway (real LANs),
        /// then the rest. Loopback, link-local (169.254) and tunnel adapters are
        /// skipped. Lets the user pick another address when the machine has
        /// several (VPNs, virtual/host-only adapters).
        /// </summary>
        public static IReadOnlyList<string> Addresses()
        {
            string routed = LocalIP();
            var scored = new List<(int rank, string ip)>();

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

                var props = ni.GetIPProperties();
                bool hasGateway = props.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork &&
                    g.Address.GetAddressBytes().Any(b => b != 0));

                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(ua.Address)) continue;
                    string ip = ua.Address.ToString();
                    if (ip.StartsWith("169.254.")) continue;               // APIPA / link-local
                    if (scored.Any(s => s.ip == ip)) continue;
                    int rank = ip == routed ? 0 : (hasGateway ? 1 : 2);    // routed, real LAN, then virtual
                    scored.Add((rank, ip));
                }
            }

            if (scored.Count == 0) return new[] { routed };
            return scored.OrderBy(s => s.rank).Select(s => s.ip).ToList();
        }

        void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch { break; }
                ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
            }
        }

        void HandleClient(TcpClient client)
        {
            try
            {
                using (client)
                using (var ssl = new SslStream(client.GetStream(), false))
                {
                    ssl.AuthenticateAsServer(_cert, false,
                        SslProtocols.Tls12 | SslProtocols.Tls13, false);

                    string? path = ReadRequestPath(ssl);
                    if (path == null) return;

                    switch (path)
                    {
                        case "/": ServeEmbedded(ssl, "index.html", "text/html; charset=utf-8"); break;
                        case "/app.js": ServeEmbedded(ssl, "app.js", "application/javascript"); break;
                        case "/colormap": ServeText(ssl, ImpedanceSource.GetColorMapJson(), "application/json"); break;
                        case "/stream": ServeStream(ssl); break;
                        default: WriteResponse(ssl, "404 Not Found", "text/plain", Encoding.UTF8.GetBytes("Not found")); break;
                    }
                }
            }
            catch { /* TLS handshake failed, client disconnected, or shutting down */ }
        }

        /// <summary>Reads the request headers and returns the path (query stripped), or null.</summary>
        static string? ReadRequestPath(SslStream ssl)
        {
            var buf = new byte[8192];
            int total = 0, term = -1;
            while (total < buf.Length)
            {
                int n = ssl.Read(buf, total, buf.Length - total);
                if (n <= 0) break;
                total += n;
                for (int i = 3; i < total; i++)
                    if (buf[i - 3] == '\r' && buf[i - 2] == '\n' && buf[i - 1] == '\r' && buf[i] == '\n') { term = i; break; }
                if (term >= 0) break;
            }
            if (total == 0) return null;

            string head = Encoding.ASCII.GetString(buf, 0, total);
            int eol = head.IndexOf('\n');
            if (eol < 0) return null;
            var parts = head[..eol].Trim().Split(' ');   // GET /path?query HTTP/1.1
            if (parts.Length < 2) return null;

            string target = parts[1];
            int q = target.IndexOf('?');
            return q >= 0 ? target[..q] : target;
        }

        void ServeEmbedded(SslStream ssl, string name, string contentType)
        {
            using var src = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Charmeleon.{name}");
            if (src == null) { WriteResponse(ssl, "404 Not Found", "text/plain", Encoding.UTF8.GetBytes("Not found")); return; }
            using var ms = new MemoryStream();
            src.CopyTo(ms);
            WriteResponse(ssl, "200 OK", contentType, ms.ToArray());
        }

        static void ServeText(SslStream ssl, string body, string contentType)
            => WriteResponse(ssl, "200 OK", contentType, Encoding.UTF8.GetBytes(body));

        static void WriteResponse(SslStream ssl, string status, string contentType, byte[] body)
        {
            var head = $"HTTP/1.1 {status}\r\n" +
                       $"Content-Type: {contentType}\r\n" +
                       $"Content-Length: {body.Length}\r\n" +
                       "Cache-Control: no-cache\r\n" +
                       "Connection: close\r\n\r\n";
            var hb = Encoding.ASCII.GetBytes(head);
            ssl.Write(hb, 0, hb.Length);
            ssl.Write(body, 0, body.Length);
            ssl.Flush();
        }

        void ServeStream(SslStream ssl)
        {
            // Server-Sent Events. Runs until the client disconnects or the server
            // shuts down; the write then throws and is swallowed by HandleClient,
            // so nothing escapes to the global unhandled-exception handler.
            var head = "HTTP/1.1 200 OK\r\n" +
                       "Content-Type: text/event-stream\r\n" +
                       "Cache-Control: no-cache\r\n" +
                       "X-Accel-Buffering: no\r\n" +
                       "Connection: close\r\n\r\n";
            var hb = Encoding.ASCII.GetBytes(head);
            ssl.Write(hb, 0, hb.Length);
            ssl.Flush();

            while (_running)
            {
                var msg = Encoding.UTF8.GetBytes($"data: {ImpedanceSource.GetJson()}\n\n");
                ssl.Write(msg, 0, msg.Length);
                ssl.Flush();
                Thread.Sleep(1000);
            }
        }

        // ------------------------------------------------------------------ //
        //  Self-signed certificate (cached so a device only warns once)
        // ------------------------------------------------------------------ //

        static X509Certificate2 LoadOrCreateCertificate()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Charmeleon");
            string path = Path.Combine(dir, "webcert.pfx");

            if (File.Exists(path))
            {
                try
                {
                    var existing = new X509Certificate2(path, (string?)null, X509KeyStorageFlags.Exportable);
                    if (existing.NotAfter > DateTime.Now.AddDays(1)) return existing;
                }
                catch { /* unreadable or wrong format: regenerate below */ }
            }

            var cert = CreateSelfSignedCertificate(LocalIP());
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, cert.Export(X509ContentType.Pfx));
            }
            catch { /* non-fatal: use the in-memory certificate for this run */ }
            return cert;
        }

        static X509Certificate2 CreateSelfSignedCertificate(string host)
        {
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest("CN=Charmeleon", rsa,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false)); // serverAuth

            var san = new SubjectAlternativeNameBuilder();
            san.AddDnsName("localhost");
            san.AddIpAddress(IPAddress.Loopback);
            if (IPAddress.TryParse(host, out var ip)) san.AddIpAddress(ip);
            req.CertificateExtensions.Add(san.Build());

            using var created = req.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));

            // Re-import via PFX so the private key is usable by SslStream on Windows.
            return new X509Certificate2(created.Export(X509ContentType.Pfx), (string?)null,
                X509KeyStorageFlags.Exportable);
        }

        public void Dispose()
        {
            _running = false;
            try { _listener.Stop(); } catch { }
            try { _cert?.Dispose(); } catch { }
        }
    }
}
