using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices.ComTypes {
    [Guid(IID.IShellItem)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem {
    }
}
