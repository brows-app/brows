using Domore.Logs;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Url {
    internal sealed class UrlHost {
        private static readonly ILog Log = Logging.For(typeof(UrlHost));

        private static async Task<UrlHost> Lookup(string host, UriHostNameType hostType, CancellationToken token) {
            switch (hostType) {
                case UriHostNameType.Dns:
                    var addresses = await Dns.GetHostAddressesAsync(host, token).ConfigureAwait(false);
                    var v4Address = addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork);
                    if (v4Address != null) {
                        return new(v4Address.MapToIPv4(), AddressFamily.InterNetwork);
                    }
                    var v6Address = addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetworkV6);
                    if (v6Address != null) {
                        return new(v6Address.MapToIPv6(), AddressFamily.InterNetworkV6);
                    }
                    throw new ArgumentException(paramName: nameof(host), message: $"No IP address found for host '{host}'.");
                case UriHostNameType.IPv4:
                    return new(IPAddress.Parse(host), AddressFamily.InterNetwork);
                case UriHostNameType.IPv6:
                    return new(IPAddress.Parse(host), AddressFamily.InterNetworkV6);
                default:
                    throw new ArgumentException(paramName: nameof(hostType), message: $"Invalid host type, '{hostType}', of host '{host}'.");
            }
        }

        public IPAddress Address { get; }
        public AddressFamily AddressFamily { get; }

        public UrlHost(IPAddress address, AddressFamily addressFamily) {
            Address = address ?? throw new ArgumentNullException(nameof(address));
            AddressFamily = addressFamily;
        }

        public sealed override string ToString() {
            return Address.ToString();
        }

        public Uri Substitute(Uri uri) {
            ArgumentNullException.ThrowIfNull(uri);
            var b = new UriBuilder(uri) {
                Host = Address.ToString()
            };
            return b.Uri;
        }

        public static async Task<UrlHost> Lookup(Uri uri, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(uri);
            if (Log.Info()) {
                Log.Info(Log.Join("Looking up", uri));
            }
            var result = await Lookup(uri.Host, uri.HostNameType, token).ConfigureAwait(false);
            if (Log.Info()) {
                Log.Info(Log.Join("Looked up", uri, "and found", result));
            }
            return result;
        }

        public static async Task<Uri> Resolve(Uri uri, CancellationToken token) {
            if (Log.Info()) {
                Log.Info(Log.Join("Resolving", uri));
            }
            var host = await Lookup(uri, token).ConfigureAwait(false);
            var result = host.Substitute(uri);
            if (Log.Info()) {
                Log.Info(Log.Join("Resolved", uri, "to", result));
            }
            return result;
        }
    }
}
