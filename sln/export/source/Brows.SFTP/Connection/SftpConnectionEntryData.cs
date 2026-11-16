using System;
using System.Threading.Tasks;

namespace Brows.Data;

internal static class SftpConnectionEntryData {
    public sealed class Kind : Definition<string> {
        public Kind() : base(entry => entry.Kind) {
            Width = 500;
        }
    }

    public sealed class Order : Definition<int> {
        public Order() : base(entry => entry.Order) {
        }
    }

    public sealed class Data : Definition<object> {
        public Data() : base(entry => entry.Data) {
        }
    }

    internal abstract class Definition<T> : EntryDataExport<SftpConnectionEntry, T> {
        private Func<SftpConnectionEntry, T> Value { get; }
        public sealed override string Group => nameof(SftpConnectionEntryData);

        protected Definition(Func<SftpConnectionEntry, T> value) {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        protected sealed override Task<T> GetValue(SftpConnectionEntry entry, Action<T> progress, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(entry);
            return Task.FromResult(Value(entry));
        }
    }
}
