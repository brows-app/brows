using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    using InteropServices.ComTypes;

    public static class shell32 {
        public const int MAX_PATH = 260;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
        public static extern bool ShellExecuteExW([In, Out] ref SHELLEXECUTEINFOW lpExecInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern IntPtr SHGetFileInfoW(
            [In] string pszPath,
            FILE_ATTRIBUTE dwFileAttributes,
            [In, Out] ref SHFILEINFOW psfi,
            uint cbFileInfo,
            SHGFI uFlags);

        [DllImport("shell32.dll", PreserveSig = true)]
        public static extern HRESULT SHGetStockIconInfo(
            SHSTOCKICONID siid,
            SHGSI uFlags,
            [In, Out] ref SHSTOCKICONINFO psii);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern HRESULT SHCreateItemFromParsingName(
            [In] string pszPath,
            [In, Optional] IntPtr pbc,
            [In] ref Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItem ppv);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern IntPtr ExtractIconW(
            [In] IntPtr hInst,
            [In] string pszExeFileName,
            uint nIconIndex);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern HRESULT SHGetPropertyStoreFromParsingName(
            [In] string pszPath,
            [In, Optional] IntPtr pbc,
            [In] GETPROPERTYSTOREFLAGS flags,
            [In] ref Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 3)] out IPropertyStore ppv);
    }
}
