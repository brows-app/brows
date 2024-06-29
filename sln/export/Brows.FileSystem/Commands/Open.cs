using Brows.Exports;
using Brows.FileSystem;
using Domore.Conf.Cli;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class Open : FileSystemCommand<Open.Parameter> {
        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(FileSystemEntry),
            typeof(FileSystemTreeNode)
        };

        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == context.HasSource(out IFileSystemInfo _, out var items)) return false;
            if (false == context.GetParameter(out var parameter)) {
                return false;
            }
            var with = parameter.With;
            var where = parameter.Where ?? CommandContextProvide.ActivePanel;
            return context.Operate(async (progress, token) => {
                async Task<bool> open(FileSystemInfo info, CancellationToken token) {
                    var provided = await context.Provide(id: info.FullName, where, token);
                    if (provided) {
                        return true;
                    }
                    var file = info as FileInfo;
                    if (file != null) {
                        var linkService = LinkFile;
                        if (linkService != null) {
                            var link = default(string);
                            var linked = await linkService.Work(file.FullName, value => link = value, token);
                            if (linked) {
                                var linkProvided = await context.Provide(id: link, where, token);
                                if (linkProvided) {
                                    return true;
                                }
                            }
                        }
                        if (string.IsNullOrWhiteSpace(with)) {
                            var open = OpenFile?.Work(file.FullName, token);
                            if (open != null) {
                                return await open;
                            }
                        }
                        else {
                            var open = OpenFileWith?.Work(file.FullName, with, token);
                            if (open != null) {
                                return await open;
                            }
                        }
                    }
                    return false;
                }
                var worked = false;
                var infos = items.Select(item => item?.Info).Where(info => info != null);
                foreach (var info in infos) {
                    worked |= await open(info, token);
                }
                return worked;
            });
        }

        public ILinkFile LinkFile { get; set; }
        public IOpenFile OpenFile { get; set; }
        public IOpenFileWith OpenFileWith { get; set; }

        public sealed class Parameter {
            [CliArgument]
            public CommandContextProvide? Where { get; set; }
            public string With { get; set; }
        }
    }
}
