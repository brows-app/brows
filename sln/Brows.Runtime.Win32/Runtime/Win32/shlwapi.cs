using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Brows.Runtime.Win32 {
    internal static partial class shlwapi {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern HRESULT AssocQueryStringW(ASSOCF flags, ASSOCSTR str, string pszAssoc, string pszExtra, [Out] StringBuilder pszOut, ref uint pcchOut);

        [DllImport("shlwapi.dll")]
        public static extern HRESULT AssocCreate(Guid clsid, ref Guid riid, out IntPtr ppv);

        [Guid("c46ca590-3c3f-11d2-bee6-0000f805ca57")]
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IQueryAssociations {
            [PreserveSig] HRESULT Init(ASSOCF flags, [MarshalAs(UnmanagedType.LPWStr)] string pszAssoc, IntPtr hkProgid, IntPtr hwnd);
            [PreserveSig] HRESULT GetString(ASSOCF flags, ASSOCSTR str, [MarshalAs(UnmanagedType.LPWStr)] string pszExtra, [Out] StringBuilder pszOut, ref uint pcchOut);
        }
    }
}
