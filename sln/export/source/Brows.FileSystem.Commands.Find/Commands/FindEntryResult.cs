using System.IO;

namespace Brows.Commands;

internal sealed record FindEntryResult(
    FileSystemInfo Info,
    FoundInInfo[] Rows,
    bool Tried = false,
    bool Matched = false,
    int Errors = 0,
    int Skipped = 0,
    bool Truncated = false);
