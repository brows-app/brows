using System;
using System.Collections.Generic;

namespace Brows;

/// <summary>
/// Holds the commands that act on the messages of a mailbox.
/// </summary>
internal abstract class MBoxCommand<TParameter> : Command<TParameter> where TParameter : new() {
    protected sealed override Type Provider =>
        typeof(MBoxProvider);

    protected override IEnumerable<Type> Source => new[] {
        typeof(IEntryObservation),
        typeof(MBoxEntry),
    };
}
