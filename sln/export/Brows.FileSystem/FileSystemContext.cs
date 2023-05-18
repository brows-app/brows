using Brows.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Brows {
    public static class FileSystemContext {
        public static bool HasSourceFileSystemInfo(this ICommandContext commandContext, out FileSystemInfo info, out IReadOnlyList<FileSystemInfo> infos) {
            if (null == commandContext) throw new ArgumentNullException(nameof(commandContext));
            if (false == commandContext.HasSource(out IFileSystemInfo item, out var items)) {
                info = null;
                infos = null;
                return false;
            }
            info = item.Info;
            infos = items.OfType<IFileSystemInfo>().Select(item => item.Info).Where(info => info != null).ToList();
            return info != null && infos.Count > 0;
        }

        public static bool HasSourceFileSystemDirectory(this ICommandContext commandContext, out DirectoryInfo directory) {
            if (null == commandContext) throw new ArgumentNullException(nameof(commandContext));
            if (false == commandContext.HasSource(out IFileSystemInfo item, out var items)) {
                if (true == commandContext.HasPanel(out var active) && active.HasProvider(out FileSystemProvider provider)) {
                    directory = provider.Directory;
                    return directory != null;
                }
                directory = null;
                return false;
            }
            var entries = items.OfType<FileSystemEntry>().ToList();
            if (entries.Count == items.Count) {
                if (entries.DistinctBy(e => e.Provider).Count() == 1) {
                    directory = entries.FirstOrDefault()?.Provider?.Directory;
                    return directory != null;
                }
            }
            var nodes = items.OfType<FileSystemTreeNode>().ToList();
            if (nodes.Count == items.Count) {
                if (nodes.Count == 1) {
                    directory = nodes[0].Info as DirectoryInfo;
                    return directory != null;
                }
                var directories = nodes.Select(node => node.Info is DirectoryInfo d ? d.Parent : node.Info is FileInfo f ? f.Directory : null);
                if (directories.DistinctBy(d => d?.FullName).Count() == 1) {
                    directory = directories.FirstOrDefault();
                    return directory != null;
                }
            }
            directory = null;
            return false;
        }
    }
}
