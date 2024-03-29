using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Domore.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IInitializeWithStream)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IInitializeWithStream {
        [PreserveSig] HRESULT Initialize(IStream pstream, [MarshalAs(UnmanagedType.U4)] STGM grfMode);
    }
}
