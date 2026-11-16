using Brows.Entries;
using System;
using System.Collections.Generic;

namespace Brows.Commands;

internal sealed class Copy : CopyIO {
    protected override Type Provider => typeof(SevenZipProvider);

    protected sealed override IEnumerable<Type> Source { get; } = new[] {
        typeof(IEntry)
    };
}
