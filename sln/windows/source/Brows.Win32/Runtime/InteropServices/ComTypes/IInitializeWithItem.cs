using System;
using System.Runtime.InteropServices;

namespace Brows.Runtime.InteropServices.ComTypes; 
using Win32;

[Guid(IID.IInitializeWithItem)]
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IInitializeWithItem {
    [PreserveSig]
    HRESULT Initialize([In] IShellItem psi, [In] STGM grfMode);
}
