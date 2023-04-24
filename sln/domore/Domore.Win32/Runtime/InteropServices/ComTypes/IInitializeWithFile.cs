using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IInitializeWithFile)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IInitializeWithFile {
        [PreserveSig] HRESULT Initialize([In][MarshalAs(UnmanagedType.LPWStr)] string pszFilePath, [In][MarshalAs(UnmanagedType.U4)] STGM grfMode);
    }
}
