using Brows.Entries;
using System;
using System.Collections.Generic;

namespace Brows.Commands;

internal sealed class ClipboardPaste : ClipboardPasteIO {
    protected override Type Provider => typeof(TarProvider);

    protected sealed override IEnumerable<Type> Source { get; } = new[] {
        typeof(IEntry),
        typeof(IEntryObservation)
    };
}
