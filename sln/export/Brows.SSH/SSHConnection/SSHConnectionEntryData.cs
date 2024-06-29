using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.SSHConnection {
    internal static class SSHConnectionEntryData {
        public abstract class Definition<T> : SSHConnectionEntryData<T> {
            protected Func<SSHConnectionEntry, T> Func { get; }

            protected Definition(Func<SSHConnectionEntry, T> func) {
                Func = func ?? throw new ArgumentNullException(nameof(func));
            }

            protected sealed override Task<T> GetValue(SSHConnectionEntry entry, Action<T> progress, CancellationToken token) {
                ArgumentNullException.ThrowIfNull(entry);
                return Task.FromResult(Func(entry));
            }
        }

        public sealed class Kind : Definition<string> {
            public Kind() : base(e => e.Kind) {
                Width = 500;
            }
        }

        public sealed class Order : Definition<int> {
            public Order() : base(e => e.Order) {
            }
        }

        public sealed class Data : Definition<object> {
            public Data() : base(e => e.Data) {
            }
        }
    }

    internal abstract class SSHConnectionEntryData<T> : EntryDataDefinition<SSHConnectionEntry, T> {
        public sealed override string Group => nameof(SSHConnectionEntryData);
    }
}
