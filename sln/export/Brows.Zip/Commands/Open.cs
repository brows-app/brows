using Brows.Exports;
using Domore.Conf.Cli;
using Domore.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class Open : ZipCommand<Open.Parameter> {
        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(IEntry)
        };

        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) return false;
            if (context.GetParameter(out var parameter) == false) return false;
            if (active.HasProvider(out ZipProvider provider) == false) return false;
            if (active.HasSelection<ZipEntry>(out var entries) == false) {
                return false;
            }
            return context.Operate(async (progress, token) => {
                async Task<bool> openFile(ZipEntryInfo info, CancellationToken token) {
                    if (info.Name.Normalized.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
                        var id = $"{provider.Zip.ArchivePath.FullName}>{info.Name.Original}";
                        var provided = await context.Provide(id, parameter.Where, token);
                        if (provided) {
                            return true;
                        }
                    }
                    var destination = await Task.Run(cancellationToken: token, function: () => {
                        var tempPath = Path.GetTempPath();
                        for (; ; ) {
                            var tempFilePath = Path.Combine(tempPath, "Brows", nameof(ZipEntry), Guid.NewGuid().ToString(), info.Name.Top);
                            var tempFileInfo = new FileInfo(tempFilePath);
                            if (tempFileInfo.Exists == false) {
                                for (; ; ) {
                                    var directory = Directory.CreateDirectory(tempFileInfo.DirectoryName);
                                    if (directory.Exists) {
                                        break;
                                    }
                                    token.ThrowIfCancellationRequested();
                                }
                                return tempFileInfo;
                            }
                            token.ThrowIfCancellationRequested();
                        }
                    });
                    await info.Extract(destination.FullName, overwrite: false, token);
                    var extracted = await FileSystemTask.ExistingFile(destination.FullName, token);
                    if (extracted != null) {
                        var service = OpenFile;
                        if (service != null) {
                            var open = await service.Work(extracted.FullName, token);
                            if (open) {
                                return true;
                            }
                        }
                    }
                    return false;
                }
                async Task<bool> openPath(ZipEntryInfo info, CancellationToken token) {
                    var id = $"{provider.Zip.ArchivePath.FullName}>{info.Name.Normalized}";
                    return await context.Provide(id, parameter.Where, token);
                }
                var worked = false;
                var selection = entries.Select(e => e.Info);
                foreach (var info in selection) {
                    switch (info.Kind) {
                        case ZipEntryKind.File:
                            worked |= await openFile(info, token);
                            break;
                        case ZipEntryKind.Path:
                            worked |= await openPath(info, token);
                            break;
                    }
                }
                return worked;
            });
        }

        public IOpenFile OpenFile { get; set; }

        public sealed class Parameter {
            [CliArgument]
            public CommandContextProvide Where { get; set; }
        }
    }
}
