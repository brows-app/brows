using System;
using System.Runtime.InteropServices;

namespace Brows.Runtime.InteropServices.ComTypes {
    using Win32;

    internal partial class propsys {
        [Guid(IID.IInitializeWithFile)]
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IInitializeWithFile {
            [PreserveSig] HRESULT Initialize([MarshalAs(UnmanagedType.LPWStr)] string pszFilePath, [MarshalAs(UnmanagedType.U4)] STGM grfMode);
        }
    }
}
