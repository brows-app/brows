using System;

namespace Domore.Runtime.Win32 {
    [Flags]
    public enum COPY_FILE : uint {
        FAIL_IF_EXISTS = 0x00000001,
        RESTARTABLE = 0x00000002,
        OPEN_SOURCE_FOR_WRITE = 0x00000004,
        ALLOW_DECRYPTED_DESTINATION = 0x00000008
    }
}
