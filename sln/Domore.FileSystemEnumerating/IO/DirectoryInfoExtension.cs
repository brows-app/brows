using System;
using System.Collections.Generic;
using System.IO;

namespace Domore.IO {
    public static class DirectoryInfoExtension {
        private static IEnumerable<FileSystemInfo> EnumerateFilesAndAddDirectories(DirectoryInfo directoryInfo, string searchPattern, EnumerationOptions enumerationOptions, ICollection<DirectoryInfo> directories) {
            if (null == directoryInfo) throw new ArgumentNullException(nameof(directoryInfo));
            var infos = directoryInfo.EnumerateFileSystemInfos(searchPattern, enumerationOptions);
            foreach (var info in infos) {
                if (info is FileInfo file) {
                    yield return file;
                }
                if (info is DirectoryInfo directory) {
                    directories.Add(directory);
                }
            }
        }

        private static IEnumerable<FileSystemInfo> EnumerateBreadthFirst(DirectoryInfo directoryInfo, string searchPattern, EnumerationOptions enumerationOptions) {
            var directories = new List<DirectoryInfo>();
            foreach (var item in EnumerateFilesAndAddDirectories(directoryInfo, searchPattern, enumerationOptions, directories)) {
                yield return item;
            }
            while (directories.Count > 0) {
                var nextDepth = new List<DirectoryInfo>();
                foreach (var directory in directories) {
                    yield return directory;
                    nextDepth.Add(directory);
                }
                directories.Clear();
                foreach (var directory in nextDepth) {
                    foreach (var info in EnumerateFilesAndAddDirectories(directory, searchPattern, enumerationOptions, directories)) {
                        yield return info;
                    }
                }
            }
        }

        public static IEnumerable<FileSystemInfo> EnumerateFileSystemInfosBreadthFirst(this DirectoryInfo directoryInfo, string searchPattern, EnumerationOptions enumerationOptions) {
            if (null == directoryInfo) throw new ArgumentNullException(nameof(directoryInfo));
            if (enumerationOptions != null) {
                enumerationOptions = new EnumerationOptions {
                    AttributesToSkip = enumerationOptions.AttributesToSkip,
                    BufferSize = enumerationOptions.BufferSize,
                    IgnoreInaccessible = enumerationOptions.IgnoreInaccessible,
                    MatchCasing = enumerationOptions.MatchCasing,
                    MatchType = enumerationOptions.MatchType,
                    MaxRecursionDepth = 0,
                    RecurseSubdirectories = false,
                    ReturnSpecialDirectories = enumerationOptions.ReturnSpecialDirectories
                };
            }
            return EnumerateBreadthFirst(directoryInfo, searchPattern, enumerationOptions);
        }
    }
}
