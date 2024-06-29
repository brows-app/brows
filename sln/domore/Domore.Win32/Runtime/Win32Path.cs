using Domore.Runtime.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Domore.Runtime {
    public static class Win32Path {
        public static bool AreSame(string path1, string path2) {
            using (var hFile1 = kernel32.CreateFileW(path1, FileAccess.Read, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero)) {
                if (hFile1.IsInvalid) {
                    throw new Win32Exception();
                }
                using (var hFile2 = kernel32.CreateFileW(path2, FileAccess.Read, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero)) {
                    if (hFile2.IsInvalid) {
                        throw new Win32Exception();
                    }
                    var fileInfo1 = default(BY_HANDLE_FILE_INFORMATION);
                    var result1 = kernel32.GetFileInformationByHandle(hFile1, out fileInfo1);
                    if (result1 == false) {
                        throw new Win32Exception();
                    }
                    var fileInfo2 = default(BY_HANDLE_FILE_INFORMATION);
                    var result2 = kernel32.GetFileInformationByHandle(hFile2, out fileInfo2);
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

        public static bool IsCaseSensitive(string path) {
            const uint FILE_CS_FLAG_CASE_SENSITIVE_DIR = 0x00000001;
            var hFile = kernel32.CreateFileW(
                filename: path,
                access: 0,
                share: FileShare.ReadWrite,
                securityAttributes: IntPtr.Zero,
                creationDisposition: FileMode.Open,
                flagsAndAttributes: (FileAttributes)FILE_FLAG.BACKUP_SEMANTICS,
                templateFile: IntPtr.Zero);
            using (hFile) {
                if (hFile.IsInvalid) {
                    throw new Win32Exception();
                }
                var iosb = new IO_STATUS_BLOCK();
                var fcsi = new FILE_CASE_SENSITIVE_INFORMATION();
                var size = (uint)Marshal.SizeOf<FILE_CASE_SENSITIVE_INFORMATION>();
                var status = ntdll.NtQueryInformationFile(
                    hFile,
                    ref iosb,
                    ref fcsi,
                    size,
                    FILE_INFORMATION_CLASS.FileCaseSensitiveInformation);
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

        public static string GetCasing(string path) {
            var dir = new DirectoryInfo(path);
            var parts = new List<string>();
            var parent = dir.Parent;
            var options = new EnumerationOptions {
                AttributesToSkip = 0,
                IgnoreInaccessible = false,
                MatchCasing = MatchCasing.CaseInsensitive,
                RecurseSubdirectories = false
            };
            while (parent != null) {
                var part = parent.EnumerateFileSystemInfos(dir.Name, options).FirstOrDefault();
                if (part != null) {
                    parts.Add(part.Name);
                }
                dir = parent;
                parent = dir.Parent;
            }
            var root = dir.FullName;
            if (root.Contains(':')) {
                root = root.ToUpper();
            }
            else {
                root = string.Join("\\", root.Split('\\').Select(part => part.ToUpper()));
            }
            parts.Add(root);
            parts.Reverse();
            return Path.Combine(parts.ToArray());
        }
    }
}
