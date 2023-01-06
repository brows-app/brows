using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    public static class ntdll {
        [DllImport("ntdll.dll")]
        [return: MarshalAs(UnmanagedType.U4)]
        public static extern NTSTATUS NtQueryInformationFile(
            [In] SafeHandle FileHandle,
            [In, Out] ref IO_STATUS_BLOCK IoStatusBlock,
            [In, Out] ref FILE_CASE_SENSITIVE_INFORMATION FileInformation,
            [In] uint Length,
            [In] FILE_INFORMATION_CLASS FileInformationClass);
    }
}
