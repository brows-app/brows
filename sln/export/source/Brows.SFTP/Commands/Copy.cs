using System;
using System.Collections.Generic;

namespace Brows.Commands;

internal sealed class Copy : CopyIO {
    protected sealed override Type Provider => typeof(SftpProvider);
    protected sealed override IEnumerable<Type> Source { get; } = [typeof(IEntry)];
}
