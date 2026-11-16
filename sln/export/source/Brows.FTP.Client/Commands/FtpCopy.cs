using System.Collections.Generic;

namespace Brows.Commands;

internal abstract class FtpCopy<TProvider> : CopyIO where TProvider : FtpProviderBase {
    protected sealed override Type Provider => typeof(TProvider);

    protected sealed override IEnumerable<Type> Source { get; } = new[] {
        typeof(FtpEntry)
    };
}
