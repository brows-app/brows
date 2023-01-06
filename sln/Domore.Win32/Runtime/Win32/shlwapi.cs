using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Domore.Runtime.Win32 {
    public static partial class shlwapi {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern HRESULT AssocQueryStringW(
            [In] ASSOCF flags,
            [In] ASSOCSTR str,
            [In] string pszAssoc,
            [In, Optional] string pszExtra,
            [Out, Optional] StringBuilder pszOut,
            [In, Out] ref uint pcchOut);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern HRESULT AssocCreate(Guid clsid, ref Guid riid, out IntPtr ppv);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern HRESULT SHCreateStreamOnFileEx(
          [In] string pszFile,
          [In] STGM grfMode,
          [In] uint dwAttributes,
          [In] bool fCreate,
          [In, Optional] IStream pstmTemplate,
          [In, Out] ref IStream ppstm);
    }
}
