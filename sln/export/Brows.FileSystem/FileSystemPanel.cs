using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Brows {
    public static class FileSystemPanel {
        public static bool HasFileSystemInfo(this IPanel panel, out IReadOnlyList<FileSystemInfo> info) {
            if (panel is null) throw new ArgumentNullException(nameof(panel));
            if (panel.HasProvider(out FileSystemProvider _) == false) {
                info = null;
                return false;
            }
            if (panel.HasEntries<FileSystemEntry>(out var entries) == false) {
                info = null;
                return false;
            }
            info = entries.Select(entry => entry.Info).ToList();
            return info.Count > 0;
        }

        public static bool HasFileSystemSelection(this IPanel panel, out IReadOnlyList<FileSystemInfo> info) {
            if (panel is null) throw new ArgumentNullException(nameof(panel));
            if (panel.HasProvider(out FileSystemProvider _) == false) {
                info = null;
                return false;
            }
            if (panel.HasSelection(out var entries) == false) {
                info = null;
                return false;
            }
            info = entries
                .OfType<FileSystemEntry>()
                .Select(entry => entry.Info)
                .ToList();
            return info.Count > 0;
        }

        public static bool HasFileSystemDirectory(this IPanel panel, out DirectoryInfo directory) {
            if (panel is null) throw new ArgumentNullException(nameof(panel));
            if (panel.HasProvider(out FileSystemProvider provider) == false) {
                directory = null;
                return false;
            }
            directory = provider.Directory;
            return directory != null;
        }

        public static bool HasFileSystemCaseSensitivity(this IPanel panel, out bool? caseSensitive) {
            if (panel is null) throw new ArgumentNullException(nameof(panel));
            if (panel.HasProvider(out FileSystemProvider provider) == false) {
                caseSensitive = null;
                return false;
            }
            caseSensitive = provider.CaseSensitive;
            return true;
        }

        public static bool HasFileSystemStreamSet(this IPanel panel, out IEntryStreamSet streamSet) {
            if (panel is null) throw new ArgumentNullException(nameof(panel));
            if (panel.HasProvider(out FileSystemProvider provider) == false) {
                streamSet = null;
                return false;
            }
            if (panel.HasSelection(out var entries) == false) {
                streamSet = null;
                return false;
            }
            var collection = entries.OfType<FileSystemEntry>().Select(e => new FileSystemStreamSource(e)).ToList();
            if (collection.Count == 0) {
                streamSet = null;
                return false;
            }
            streamSet = new FileSystemStreamSet(collection);
            return true;
        }
    }
}
