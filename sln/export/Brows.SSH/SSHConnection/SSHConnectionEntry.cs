using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.SSHConnection {
    internal sealed class SSHConnectionEntry : Entry<SSHConnectionProvider> {
        public int Order { get; }
        public string Kind { get; }
        public object Data { get; }
        public Func<CancellationToken, Task<bool>> Task { get; }

        public SSHConnectionEntry(SSHConnectionProvider provider, int order, string kind, object data = null, Func<CancellationToken, Task<bool>> task = null) : base(provider) {
            Kind = kind;
            Data = data;
            Task = task;
            Order = order;
        }

        public sealed override string ID =>
            $"{nameof(SSHConnectionEntry)}.{Kind}";
    }
}
