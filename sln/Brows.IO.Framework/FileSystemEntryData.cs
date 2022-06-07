using System.Collections.Generic;
using System.IO;

namespace Brows {
    public static class FileSystemEntryData {
        public static IReadOnlyDictionary<string, IEntryColumn> Columns =>
            FileSystemInfoWrapper.Columns;

        public static IReadOnlySet<string> Options =>
            FileSystemInfoWrapper.Keys;

        public static IReadOnlySet<string> Defaults { get; } = new HashSet<string> {
            nameof(FileSystemInfo.Name),
            nameof(FileInfo.Length),
            nameof(FileSystemInfo.LastWriteTime),
            nameof(FileSystemInfo.Extension)
        };

        public static IComponentResourceKey Resolver { get; } = new FileSystemComponentKey();
    }
}
