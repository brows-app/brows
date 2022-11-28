using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Runtime.Win32;
    using Threading.Tasks;

    internal static class Win32Path {
        public static async Task<bool> AreSame(string path1, string path2, CancellationToken cancellationToken) {
            var uri1 = new Uri(path1);
            var uri2 = new Uri(path2);
            if (uri2.Equals(uri1)) return true;

            var path1Exists = await Async.Run(cancellationToken, () => File.Exists(path1));
            if (path1Exists == false) return false;

            var path2Exists = await Async.Run(cancellationToken, () => File.Exists(path2));
            if (path2Exists == false) return false;

            using (var hFile1 = await Async.Run(cancellationToken, () => kernel32.CreateFileW(path1, FileAccess.Read, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero))) {
                if (hFile1.IsInvalid) {
                    throw new Win32Exception();
                }
                using (var hFile2 = await Async.Run(cancellationToken, () => kernel32.CreateFileW(path2, FileAccess.Read, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero))) {
                    if (hFile2.IsInvalid) {
                        throw new Win32Exception();
                    }
                    var fileInfo1 = default(BY_HANDLE_FILE_INFORMATION);
                    var result1 = await Async.Run(cancellationToken, () => kernel32.GetFileInformationByHandle(hFile1, out fileInfo1));
                    if (result1 == false) {
                        throw new Win32Exception();
                    }
                    var fileInfo2 = default(BY_HANDLE_FILE_INFORMATION);
                    var result2 = await Async.Run(cancellationToken, () => kernel32.GetFileInformationByHandle(hFile2, out fileInfo2));
                    if (result2 == false) {
                        throw new Win32Exception();
                    }
                    return
                        fileInfo1.VolumeSerialNumber == fileInfo2.VolumeSerialNumber &&
                        fileInfo1.FileIndexHigh == fileInfo2.FileIndexHigh &&
                        fileInfo1.FileIndexLow == fileInfo2.FileIndexLow;
                }
            }
        }

        public static async Task<bool> IsCaseSensitive(string path, CancellationToken cancellationToken) {
            const uint FILE_CS_FLAG_CASE_SENSITIVE_DIR = 0x00000001;
            var hFile = await Async.Run(cancellationToken, () => kernel32.CreateFileW(
                filename: path,
                access: 0,
                share: FileShare.ReadWrite,
                securityAttributes: IntPtr.Zero,
                creationDisposition: FileMode.Open,
                flagsAndAttributes: (FileAttributes)FILE_FLAG.BACKUP_SEMANTICS,
                templateFile: IntPtr.Zero));
            using (hFile) {
                var iosb = new IO_STATUS_BLOCK();
                var fcsi = new FILE_CASE_SENSITIVE_INFORMATION();
                var size = (uint)Marshal.SizeOf<FILE_CASE_SENSITIVE_INFORMATION>();
                var status = await Async.Run(cancellationToken, () => ntdll.NtQueryInformationFile(
                    hFile,
                    ref iosb,
                    ref fcsi,
                    size,
                    FILE_INFORMATION_CLASS.FileCaseSensitiveInformation));
                switch (status) {
                    case NTSTATUS.STATUS_SUCCESS:
                        return FILE_CS_FLAG_CASE_SENSITIVE_DIR == (fcsi.Flags & FILE_CS_FLAG_CASE_SENSITIVE_DIR);
                    case NTSTATUS.STATUS_NOT_IMPLEMENTED:
                    case NTSTATUS.STATUS_NOT_SUPPORTED:
                    case NTSTATUS.STATUS_INVALID_INFO_CLASS:
                    case NTSTATUS.STATUS_INVALID_PARAMETER:
                        return false;
                    default:
                        // TODO: An error may have occurred here. Possibly throw an exception.
                        return false;
                }
            }
        }
    }
}
