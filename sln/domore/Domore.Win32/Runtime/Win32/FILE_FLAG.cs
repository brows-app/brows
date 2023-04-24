﻿using System;

namespace Domore.Runtime.Win32 {
    [Flags]
    public enum FILE_FLAG : uint {
        BACKUP_SEMANTICS = 0x02000000,
        DELETE_ON_CLOSE = 0x04000000,
        NO_BUFFERING = 0x20000000,
        OPEN_NO_RECALL = 0x00100000,
        OPEN_REPARSE_POINT = 0x00200000,
        OVERLAPPED = 0x40000000,
        POSIX_SEMANTICS = 0x01000000,
        RANDOM_ACCESS = 0x10000000,
        SESSION_AWARE = 0x00800000,
        SEQUENTIAL_SCAN = 0x08000000,
        WRITE_THROUGH = 0x80000000
    }
}
