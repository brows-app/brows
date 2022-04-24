using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO.Extensions {
    using Runtime.Win32;
    using Threading.Tasks;

    internal static class PathAsync {
        public static async Task<bool> IsSamePathOrFileAsync(string path1, string path2, CancellationToken cancellationToken) {
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
    }
}
