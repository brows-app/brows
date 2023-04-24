namespace Domore.Runtime.Win32 {
    public enum FILE_NOTIFY_CHANGE : uint {
        FILE_NAME = 0x00000001,
        DIR_NAME = 0x00000002,
        ATTRIBUTES = 0x00000004,
        SIZE = 0x00000008,
        LAST_WRITE = 0x00000010,
        SECURITY = 0x00000100,
    }
}
