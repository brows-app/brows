using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Domore.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IQueryAssociations)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IQueryAssociations {
        [PreserveSig]
        HRESULT Init(
            [In] ASSOCF flags,
            [In, Optional, MarshalAs(UnmanagedType.LPWStr)] string pszAssoc,
            [In, Optional] IntPtr hkProgid,
            [In, Optional] IntPtr hwnd);

        [PreserveSig]
        HRESULT GetString(
            [In] ASSOCF flags,
            [In] ASSOCSTR str,
            [In, Optional, MarshalAs(UnmanagedType.LPWStr)] string pszExtra,
            [Out, Optional, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszOut,
            [In, Out] ref uint pcchOut);
    }
}
