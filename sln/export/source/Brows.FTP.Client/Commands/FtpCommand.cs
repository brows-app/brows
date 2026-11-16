using System.Collections.Generic;

namespace Brows.Commands;

internal abstract class FtpCommand<TProvider, TParameter> : Command<TParameter>
where TProvider : FtpProviderBase
where TParameter : new() {
    protected sealed override Type Provider => typeof(TProvider);

    protected override IEnumerable<Type> Source => new[] {
        typeof(IEntryObservation),
        typeof(FtpEntry)
    };
}
