using Domore.Collections.Generic;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO.Compression {
    internal class CompressionInput {
        private static readonly ILog Log = Logging.For(typeof(CompressionInput));

        private static StringComparer PathComparer =>
            _PathComparer ?? (
            _PathComparer = Environment.OSVersion.Platform == PlatformID.Win32NT
                ? StringComparer.CurrentCultureIgnoreCase
                : StringComparer.CurrentCulture);
        private static StringComparer _PathComparer;

        public async Task<CompressionInputFileCollection> Files(IOperationProgress progress, IEnumerable<IEntry> entries, CancellationToken cancellationToken) {
            if (null == entries) throw new ArgumentNullException(nameof(entries));
            if (null == progress) throw new ArgumentNullException(nameof(progress));
            var files = new CompressionInputFileCollection(PathComparer);
            var info = progress.Info;
            var
            target = progress.Target;
            target.Add(entries.Count());
            foreach (var entry in entries) {
                var path = entry.File;
                if (path == null) {
                    if (Log.Info()) {
                        Log.Info(
                            entry.ID,
                            nameof(entry.File) + " > <null>");
                    }
                    continue;
                }

                var dir = await FileSystem.DirectoryExists(path, cancellationToken);
                if (dir != null) {
                    var dirEnum = dir.EnumerateFiles(searchPattern: "*", enumerationOptions: new EnumerationOptions {
                        AttributesToSkip = 0,
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = true,
                        ReturnSpecialDirectories = false
                    });
                    var dirAsync = dirEnum.CollectAsync(cancellationToken);
                    var dirFiles = dirAsync.FlattenAsync(cancellationToken);
                    await foreach (var dirFile in dirFiles) {
                        target.Add(1);
                        info.Data("Add {0}", dirFile.Name);
                        if (Log.Info()) {
                            Log.Info($"{nameof(files.Add)} > {dirFile.FullName}");
                        }
                        await files.Add(dirFile, cancellationToken);
                        progress.Add(1);
                    }
                }
                var file = await FileSystem.FileExists(path, cancellationToken);
                if (file != null) {
                    if (Log.Info()) {
                        Log.Info($"{nameof(files.Add)} > {file.FullName}");
                    }
                    info.Data("Add {0}", file.Name);
                    await files.Add(file, cancellationToken);
                }
                progress.Add(1);
            }
            return files;
        }
    }
}
