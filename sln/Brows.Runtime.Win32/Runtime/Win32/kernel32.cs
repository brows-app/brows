using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Brows.Runtime.Win32 {
    internal partial class kernel32 {
        public delegate PROGRESS ProgressRoutine(
            long TotalFileSize,
            long TotalBytesTransferred,
            long StreamSize,
            long StreamBytesTransferred,
            uint dwStreamNumber,
            CALLBACK dwCallbackReason,
            IntPtr hSourceFile,
            IntPtr hDestinationFile,
            IntPtr lpData);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateFileW(
            string filename,
            [MarshalAs(UnmanagedType.U4)] FileAccess access,
            [MarshalAs(UnmanagedType.U4)] FileShare share,
            IntPtr securityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
            [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CopyFileExW(
            string lpExistingFileName,
            string lpNewFileName,
            ProgressRoutine lpProgressRoutine,
            IntPtr lpData,
            ref int pbCancel,
            COPY_FILE dwCopyFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MoveFileWithProgressW(
          string lpExistingFileName,
          string lpNewFileName,
          ProgressRoutine lpProgressRoutine,
          IntPtr lpData,
          MOVEFILE dwFlags);
    }
}
