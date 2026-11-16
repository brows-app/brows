using Brows.Entries;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class SftpConnectionEntry : Entry<SftpConnectionProvider> {
    public int Order { get; }
    public string Kind { get; }
    public object Data { get; }
    public Func<CancellationToken, Task<bool>> Action { get; }

    public sealed override string ID => $"{nameof(SftpConnectionEntry)}.{Kind}";

    internal SftpConnectionEntry(SftpConnectionProvider provider,
                                 int order,
                                 string kind,
                                 object data = null,
                                 Func<CancellationToken, Task<bool>> action = null) : base(provider) {
        Order = order;
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Data = data;
        Action = action;
    }
}
