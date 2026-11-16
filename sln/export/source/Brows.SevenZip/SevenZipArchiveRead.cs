using System.Collections.Generic;

namespace Brows;

internal sealed record SevenZipArchiveRead {
    internal bool ExtractOverwrites { get; init; }

    internal Dictionary<string, string> ExtractEntriesToFiles {
        get => field ??= new();
        init;
    }
}
