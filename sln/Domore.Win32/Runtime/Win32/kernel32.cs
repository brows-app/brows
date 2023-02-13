using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    public partial class kernel32 {
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

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetModuleHandleW([In, Optional, MarshalAs(UnmanagedType.LPWStr)] string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(
            [In] IntPtr hDevice,
            [In] uint dwIoControlCode,
            [In, Optional] IntPtr lpInBuffer,
            [In] uint nInBufferSize,
            [Out, Optional] IntPtr lpOutBuffer,
            [In] uint nOutBufferSize,
            [Out, Optional] out uint lpBytesReturned,
            [In, Out, Optional] IntPtr lpOverlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindFirstChangeNotificationW(
          [In] string lpPathName,
          [In] bool bWatchSubtree,
          [In, MarshalAs(UnmanagedType.U4)] FILE_NOTIFY_CHANGE dwNotifyFilter
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FindNextChangeNotification([In] IntPtr hChangeHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FindCloseChangeNotification([In] IntPtr hChangeHandle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool ReadDirectoryChangesW(
          [In] IntPtr hDirectory,
          [Out] IntPtr lpBuffer,
          [In] uint nBufferLength,
          [In] bool bWatchSubtree,
          [In] uint dwNotifyFilter,
          [Out, Optional] out uint lpBytesReturned,
          [In, Out, Optional] IntPtr lpOverlapped,
          [In, Optional] IntPtr lpCompletionRoutine
        );

        [DllImport("kernel32.dll")]
        public static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        public static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        public static extern bool AttachConsole(int dwProcessId);
    }
}
