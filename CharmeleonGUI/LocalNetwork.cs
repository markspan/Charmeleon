using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Charmeleon
{
    /// <summary>
    /// The PC's addresses on the local network: the addresses another device on the
    /// same Wi-Fi or LAN can use to reach the Web View. Used for the URL and QR code
    /// in the Web View dialog, and for the names on the Web View's certificate.
    /// </summary>
    static class LocalNetwork
    {
        /// <summary>
        /// All usable local IPv4 addresses, best guess first: the internet-routed
        /// address leads, then adapters that have a default gateway (real LANs),
        /// then the rest. Loopback, link-local (169.254) and tunnel adapters are
        /// skipped. Lets the user pick another address when the machine has
        /// several (VPNs, virtual/host-only adapters, Wi-Fi and Ethernet at once).
        /// Empty when the PC is on no network at all.
        /// </summary>
        public static IReadOnlyList<IPAddress> Addresses()
        {
            IPAddress? routed = RoutedAddress();
            var scored = new List<(int Rank, IPAddress Address)>();

            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                if (adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

                var properties = adapter.GetIPProperties();
                bool hasGateway = properties.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !g.Address.Equals(IPAddress.Any));

                foreach (var unicast in properties.UnicastAddresses)
                {
                    IPAddress address = unicast.Address;
                    if (address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(address) || IsLinkLocal(address)) continue;
                    if (scored.Any(s => s.Address.Equals(address))) continue;

                    int rank = address.Equals(routed) ? 0 : hasGateway ? 1 : 2;   // routed, real LAN, then virtual
                    scored.Add((rank, address));
                }
            }

            return scored.OrderBy(s => s.Rank).Select(s => s.Address).ToList();
        }

        /// <summary>
        /// The local address the OS would use to reach the internet, or null when
        /// offline. Connecting a UDP socket sends nothing: it only makes the OS pick
        /// the route, and with it the local address.
        /// </summary>
        static IPAddress? RoutedAddress()
        {
            try
            {
                using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                probe.Connect("8.8.8.8", 65530);
                return ((IPEndPoint)probe.LocalEndPoint!).Address;
            }
            catch (SocketException)
            {
                return null;
            }
        }

        /// <summary>True for 169.254.x.x, the address Windows assigns itself when no DHCP server answered.</summary>
        static bool IsLinkLocal(IPAddress address)
        {
            byte[] bytes = address.GetAddressBytes();
            return bytes[0] == 169 && bytes[1] == 254;
        }
    }
}
