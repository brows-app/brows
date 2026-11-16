using Brows.Commands;
using System;
using System.Collections.Generic;

namespace Brows.Commands;

internal abstract class SftpConnectionCommand<TParameter> : Command<TParameter> where TParameter : new() {
    protected sealed override Type Provider => typeof(SftpConnectionProvider);

    protected sealed override IEnumerable<Type> Source => [
        typeof(IEntryObservation),
        typeof(SftpConnectionEntry)
    ];
}
