using System;
using System.Runtime.InteropServices;

namespace Brows.Runtime.InteropServices.ComTypes {
    [Guid(IID.IShellItem)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem {
    };
}
