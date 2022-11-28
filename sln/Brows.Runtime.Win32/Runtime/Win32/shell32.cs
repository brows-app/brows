using System;
using System.Runtime.InteropServices;

namespace Brows.Runtime.Win32 {
    using InteropServices.ComTypes;

    internal static partial class shell32 {
        public const int MAX_PATH = 260;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool ShellExecuteExW(ref SHELLEXECUTEINFOW lpExecInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SHGetFileInfoW(string pszPath, FILE_ATTRIBUTE dwFileAttributes, ref SHFILEINFOW psfi, uint cbFileInfo, SHGFI uFlags);

        [DllImport("shell32.dll")]
        public static extern HRESULT SHGetStockIconInfo(SHSTOCKICONID siid, SHGSI uFlags, ref SHSTOCKICONINFO psii);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern HRESULT SHCreateItemFromParsingName(
            string pszPath,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItem ppv);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern IntPtr ExtractIconW(
            [In] IntPtr hInst,
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszExeFileName,
            int nIconIndex);
    }
}
