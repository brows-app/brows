namespace Domore.Runtime.Win32 {
    public enum HRESULT : uint {
        S_OK = 0x00000000,
        S_FALSE = 0x00000001,
        E_ABORT = 0x80004004,
        E_ACCESSDENIED = 0x80070005,
        E_FAIL = 0x80004005,
        E_HANDLE = 0x80070006,
        E_INVALIDARG = 0x80070057,
        E_NOINTERFACE = 0x80004002,
        E_NOTIMPL = 0x80004001,
        E_OUTOFMEMORY = 0x8007000E,
        E_POINTER = 0x80004003,
        E_UNEXPECTED = 0x8000FFFF,
        CO_E_SERVER_EXEC_FAILURE = 0x80080005
    }
}
