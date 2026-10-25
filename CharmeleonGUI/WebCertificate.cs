using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Charmeleon
{
    /// <summary>
    /// The Web View's self-signed TLS certificate. It is created on first use and
    /// cached in <c>%LOCALAPPDATA%\Charmeleon\webcert.pfx</c>, so each device only
    /// has to accept it once, and it is renewed automatically when it stops fitting.
    ///
    /// A certificate fits when it:
    /// <list type="bullet">
    /// <item>names every address the PC currently has. Browsers check that the
    /// address in the URL is on the certificate, so a certificate made before the
    /// router gave the PC a new address would be refused on that address;</item>
    /// <item>is valid for at most <see cref="LifetimeDays"/> days (Apple devices
    /// refuse longer-lived server certificates outright) and still has at least
    /// <see cref="RenewDaysBefore"/> days to go;</item>
    /// <item>carries its private key.</item>
    /// </list>
    /// A renewed certificate is a new identity, so each device shows the browser's
    /// warning once more. That only happens when one of the rules above demands it.
    /// </summary>
    sealed class WebCertificate
    {
        /// <summary>Validity of a new certificate, the longest every current browser accepts.</summary>
        const int LifetimeDays = 397;

        /// <summary>A certificate this close to expiring is replaced.</summary>
        const int RenewDaysBefore = 30;

        /// <summary>Object identifier of the Subject Alternative Name extension.</summary>
        const string SubjectAltNameOid = "2.5.29.17";

        /// <summary>Object identifier of the TLS server authentication usage.</summary>
        const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";

        readonly object _lock = new();
        X509Certificate2 _certificate;
        SslStreamCertificateContext _context;

        /// <summary>Loads the cached certificate, or creates a new one when it no longer fits.</summary>
        public WebCertificate()
        {
            IReadOnlyList<IPAddress> addresses = LocalNetwork.Addresses();
            X509Certificate2? cached = LoadCached();
            string? unfit = cached is null ? "there was no usable one" : WhyUnfit(cached, addresses);

            if (unfit is null)
            {
                _certificate = cached!;
                WebLog.Write($"Certificate: using {Describe(_certificate)}");
            }
            else
            {
                cached?.Dispose();
                _certificate = CreateAndSave(addresses, unfit);
            }
            _context = ContextFor(_certificate);
        }

        /// <summary>Where the certificate is cached between runs.</summary>
        static string CachePath => Path.Combine(WebServer.DataFolder, "webcert.pfx");

        /// <summary>
        /// The certificate to present on a connection that arrived on
        /// <paramref name="localAddress"/>. If that address is not on the current
        /// certificate (the PC's address changed while Charmeleon was running), the
        /// certificate is renewed first, so the browser gets one that matches.
        /// </summary>
        public SslStreamCertificateContext For(IPAddress localAddress)
        {
            lock (_lock)
            {
                if (!Names(_certificate, localAddress))
                {
                    // Include the address explicitly: it may be one LocalNetwork skips
                    // (a tunnel or link-local adapter), and must not trigger this again.
                    // The old certificate is left to the garbage collector rather than
                    // disposed, since a handshake in progress may still be using it.
                    var addresses = LocalNetwork.Addresses().Append(localAddress).Distinct().ToList();
                    _certificate = CreateAndSave(addresses, $"{localAddress} is not on the current one");
                    _context = ContextFor(_certificate);
                }
                return _context;
            }
        }

        /// <summary>The cached certificate, or null when there is none or it cannot be read.</summary>
        static X509Certificate2? LoadCached()
        {
            if (!File.Exists(CachePath)) return null;
            try
            {
                return new X509Certificate2(CachePath, (string?)null, X509KeyStorageFlags.Exportable);
            }
            catch (CryptographicException ex)
            {
                WebLog.Write($"Certificate: the cached one cannot be read ({WebLog.Describe(ex)})");
                return null;
            }
        }

        /// <summary>Why <paramref name="certificate"/> no longer fits, or null when it does.</summary>
        static string? WhyUnfit(X509Certificate2 certificate, IReadOnlyList<IPAddress> addresses)
        {
            if (!certificate.HasPrivateKey)
                return "the cached one has no private key";
            if (certificate.NotAfter - certificate.NotBefore > TimeSpan.FromDays(LifetimeDays + 1))
                return "the cached one is valid for longer than Apple devices accept";
            if (certificate.NotAfter - DateTime.Now < TimeSpan.FromDays(RenewDaysBefore))
                return $"the cached one expires on {certificate.NotAfter:yyyy-MM-dd}";

            IPAddress? missing = addresses.FirstOrDefault(a => !Names(certificate, a));
            return missing is null ? null : $"{missing} is not on the cached one";
        }

        /// <summary>True when <paramref name="address"/> is one of the certificate's names.</summary>
        static bool Names(X509Certificate2 certificate, IPAddress address) =>
            NamedAddresses(certificate).Contains(address);

        /// <summary>The IP addresses in the certificate's Subject Alternative Name extension.</summary>
        static IEnumerable<IPAddress> NamedAddresses(X509Certificate2 certificate)
        {
            X509Extension? extension = certificate.Extensions[SubjectAltNameOid];
            return extension is null
                ? []
                : new X509SubjectAlternativeNameExtension(extension.RawData, extension.Critical).EnumerateIPAddresses();
        }

        /// <summary>Creates a certificate for <paramref name="addresses"/>, caches it and logs why.</summary>
        static X509Certificate2 CreateAndSave(IReadOnlyList<IPAddress> addresses, string reason)
        {
            X509Certificate2 certificate = Create(addresses);
            try
            {
                Directory.CreateDirectory(WebServer.DataFolder);
                File.WriteAllBytes(CachePath, certificate.Export(X509ContentType.Pfx));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
            {
                // Not fatal: this run uses the certificate from memory, the next run makes another.
                WebLog.Write($"Certificate: could not be cached ({WebLog.Describe(ex)})");
            }
            WebLog.Write($"Certificate: created {Describe(certificate)}, because {reason}");
            return certificate;
        }

        /// <summary>
        /// A new self-signed server certificate (RSA 2048, SHA-256) naming
        /// <c>localhost</c>, <c>127.0.0.1</c> and every address in
        /// <paramref name="addresses"/>.
        /// </summary>
        static X509Certificate2 Create(IReadOnlyList<IPAddress> addresses)
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=Charmeleon", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(ServerAuthOid)], false));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("localhost");
            names.AddIpAddress(IPAddress.Loopback);
            foreach (IPAddress address in addresses) names.AddIpAddress(address);
            request.CertificateExtensions.Add(names.Build());

            // Back-date a day so a device whose clock runs a little behind still accepts it.
            DateTimeOffset now = DateTimeOffset.UtcNow;
            using X509Certificate2 created = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(LifetimeDays - 1));

            // Re-import through PFX: on Windows, SslStream (Schannel) cannot use the
            // ephemeral key CreateSelfSigned returns, only a key held by a key store.
            return new X509Certificate2(created.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
        }

        /// <summary>
        /// The ready-to-use TLS form of a certificate, built once instead of on every
        /// connection. Offline: a self-signed certificate has no chain to download.
        /// </summary>
        static SslStreamCertificateContext ContextFor(X509Certificate2 certificate) =>
            SslStreamCertificateContext.Create(certificate, additionalCertificates: null, offline: true);

        /// <summary>A certificate's identity and names, for the log.</summary>
        static string Describe(X509Certificate2 certificate)
        {
            string names = string.Join(", ", NamedAddresses(certificate));
            return $"{certificate.Thumbprint[..8]}, valid until {certificate.NotAfter:yyyy-MM-dd}, for localhost, {names}";
        }
    }
}
