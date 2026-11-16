using System;

namespace Brows.Git;

[Flags]
internal enum GitStatusFlags : uint {
    Current = 0,
    IndexNew = 1,
    IndexModified = 2,
    IndexDeleted = 4,
    IndexRenamed = 8,
    IndexTypeChange = 16,
    WorkTreeNew = 128,
    WorkTreeModified = 256,
    WorkTreeDeleted = 512,
    WorkTreeTypeChange = 1024,
    WorkTreeRenamed = 2048,
    WorkTreeUnreadable = 4096,
    Ignored = 16384,
    Conflicted = 32768
}
