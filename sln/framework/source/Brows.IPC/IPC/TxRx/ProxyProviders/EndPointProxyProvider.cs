using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Brows.IPC.TxRx.ProxyProviders;

internal sealed class EndPointProxyProvider : TxRxProxyProvider {
    public IObjectTypeResolver TypeResolver { get; }
    public IReadOnlyList<IPEndPoint> EndPoints { get; }

    public EndPointProxyProvider(IObjectTypeResolver typeResolver, IEnumerable<IPEndPoint> endPoints) {
        TypeResolver = typeResolver;
        EndPoints = endPoints?.Where(item => item is not null)?.ToList() ?? [];
    }

    public sealed override void Start() {
        OnProxiesChanged(EventArgs.Empty);
    }

    public sealed override IEnumerable<TxRxProxy> GetProxies() {
        return EndPoints
            .Select(endPoint => new EndPointProxy(endPoint, TypeResolver))
            .ToList();
    }

    private sealed class EndPointProxy : TxRxProxy {
        protected sealed override ObjectClient CreateClient() {
            return ObjectClient.From(EndPoint, TypeResolver);
        }

        public IPEndPoint EndPoint { get; }
        public IObjectTypeResolver TypeResolver { get; }

        public EndPointProxy(IPEndPoint endPoint, IObjectTypeResolver typeResolver) : base(endPoint?.ToString()) {
            EndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
            TypeResolver = typeResolver;
        }
    }
}
