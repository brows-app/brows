using System;

namespace Domore.Runtime.Win32 {
    [Flags]
    public enum FILE_ATTRIBUTE : uint {
        READONLY = 0x00000001,
        HIDDEN = 0x00000002,
        SYSTEM = 0x00000004,
        DIRECTORY = 0x00000010,
        ARCHIVE = 0x00000020,
        DEVICE = 0x00000040,
        NORMAL = 0x00000080,
        TEMPORARY = 0x00000100,
        SPARSE_FILE = 0x00000200,
        REPARSE_POINT = 0x00000400,
        COMPRESSED = 0x00000800,
        OFFLINE = 0x00001000,
        NOT_CONTENT_INDEXED = 0x00002000,
        ENCRYPTED = 0x00004000,
        INTEGRITY_STREAM = 0x00008000,
        VIRTUAL = 0x00010000,
        NO_SCRUB_DATA = 0x00020000,
        EA = 0x00040000
    }
}
