using Brows.Exports;
using Domore.Conf.Cli;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class Open : FileSystemCommand<Open.Parameter> {
        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) return false;
            if (context.GetParameter(out var parameter) == false) return false;
            if (active.HasFileSystemSelection(out var selection) == false) {
                return false;
            }
            return context.Operate(async (progress, token) => {
                async Task<bool> open(FileSystemInfo info, CancellationToken token) {
                    var provided = await context.Provide(id: info.FullName, parameter.Where, token);
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
                                var linkProvided = await context.Provide(id: link.ToString(), parameter.Where, token);
                                if (linkProvided) {
                                    return true;
                                }
                            }
                        }
                        var openService = OpenFile;
                        if (openService != null) {
                            var open = await openService.Work(file.FullName, token);
                            if (open) {
                                return true;
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

        public sealed class Parameter {
            [CliArgument]
            public CommandContextProvide Where { get; set; }
        }
    }
}
