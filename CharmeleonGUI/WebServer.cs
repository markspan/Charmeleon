using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace Charmeleon
{
    /// <summary>
    /// Lightweight built-in web server (port 8765) that serves the Web View head map
    /// to any browser on the local network.  Runs on a background thread; safe to
    /// instantiate once and keep alive for the application lifetime.
    /// </summary>
    sealed class WebServer : IDisposable
    {
        const int Port = 8765;

        readonly HttpListener _listener = new();
        readonly Thread _thread = null!;
        volatile bool _running = true;

        public bool Started { get; private set; }
        public string? ErrorMessage { get; private set; }

        public WebServer()
        {
            _listener.Prefixes.Add($"http://*:{Port}/");
            try
            {
                _listener.Start();
                Started = true;
            }
            catch (HttpListenerException ex)
            {
                ErrorMessage = ex.Message;
                return;
            }

            _thread = new Thread(ServeLoop) { IsBackground = true, Name = "WebServer" };
            _thread.Start();
        }

        /// <summary>Returns the URL to display / encode as a QR code.</summary>
        public static string Url => $"http://{LocalIP()}:{Port}/";

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

        void ServeLoop()
        {
            while (_running && _listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = _listener.GetContext(); }
                catch { break; }
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
            }
        }

        void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                switch (ctx.Request.Url?.AbsolutePath)
                {
                    case "/":         ServeEmbedded(ctx, "index.html", "text/html; charset=utf-8"); break;
                    case "/app.js": ServeEmbedded(ctx, "app.js",   "application/javascript");   break;
                    case "/colormap": ServeText(ctx, ImpedanceSource.GetColorMapJson(), "application/json"); break;
                    case "/stream":   ServeStream(ctx);                                              break;
                    default:
                        ctx.Response.StatusCode = 404;
                        ctx.Response.Close();
                        break;
                }
            }
            catch { try { ctx.Response.Abort(); } catch { } }
        }

        static void ServeEmbedded(HttpListenerContext ctx, string name, string contentType)
        {
            var asm = Assembly.GetExecutingAssembly();
            using var src = asm.GetManifestResourceStream($"Charmeleon.{name}");
            if (src == null) { ctx.Response.StatusCode = 404; ctx.Response.Close(); return; }

            ctx.Response.ContentType = contentType;
            ctx.Response.AddHeader("Cache-Control", "no-cache");
            src.CopyTo(ctx.Response.OutputStream);
            ctx.Response.Close();
        }

        static void ServeText(HttpListenerContext ctx, string body, string contentType)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.ContentType = contentType;
            ctx.Response.AddHeader("Cache-Control", "no-cache");
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        void ServeStream(HttpListenerContext ctx)
        {
            // Everything here runs on a thread-pool thread. On shutdown the
            // listener is disposed, so writes and the close below will throw;
            // all of it is wrapped so nothing escapes onto the global
            // UnhandledException handler (which would pop an error dialog).
            try
            {
                ctx.Response.ContentType      = "text/event-stream";
                ctx.Response.ContentEncoding  = Encoding.UTF8;
                ctx.Response.SendChunked      = true;
                ctx.Response.AddHeader("Cache-Control",  "no-cache");
                ctx.Response.AddHeader("X-Accel-Buffering", "no");

                var writer = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8) { AutoFlush = true };
                while (_running)
                {
                    writer.Write($"data: {ImpedanceSource.GetJson()}\n\n");
                    Thread.Sleep(1000);
                }
            }
            catch { /* client disconnected or server shutting down */ }
            finally { try { ctx.Response.Close(); } catch { } }
        }

        public void Dispose()
        {
            _running = false;
            try { _listener.Stop();  } catch { }
            try { _listener.Close(); } catch { }
        }
    }
}
