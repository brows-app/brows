using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO.Compression {
    using Logger;

    internal class CompressionInput {
        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(CompressionInput)));
        private ILog _Log;

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
                var dir = await DirectoryInfoExtension.TryNewAsync(path, cancellationToken);
                if (dir != null) {
                    var dirExists = await dir.ExistsAsync(cancellationToken);
                    if (dirExists) {
                        var dirFiles = dir.RecurseFilesAsync(cancellationToken);
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
                }
                var file = await FileInfoExtension.TryNewAsync(path, cancellationToken);
                if (file != null) {
                    var fileExists = await file.ExistsAsync(cancellationToken);
                    if (fileExists) {
                        if (Log.Info()) {
                            Log.Info($"{nameof(files.Add)} > {file.FullName}");
                        }
                        info.Data("Add {0}", file.Name);
                        await files.Add(file, cancellationToken);
                    }
                }
                progress.Add(1);
            }
            return files;
        }
    }
}
