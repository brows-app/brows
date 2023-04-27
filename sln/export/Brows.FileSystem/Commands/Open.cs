using Brows.Exports;
using Domore.Conf.Cli;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class Open : FileSystemCommand<Open.Parameter> {
        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == context.GetParameter(out var parameter)) return false;
            if (false == active.HasFileSystemSelection(out var selection)) {
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
                            var link = new StringBuilder();
                            var linked = await linkService.Link(file.FullName, link, token);
                            if (linked) {
                                var linkProvided = await context.Provide(id: link.ToString(), where, token);
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
                foreach (var info in selection) {
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
