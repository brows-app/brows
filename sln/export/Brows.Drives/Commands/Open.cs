using Brows.FileSystem;
using Domore.Conf.Cli;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class Open : DriveCommand<Open.Parameter> {
        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == context.HasSource(out IFileSystemInfo _, out var items)) return false;
            if (false == context.GetParameter(out var parameter)) return false;
            return context.Operate(async (progress, token) => {
                var worked = false;
                foreach (var item in items) {
                    var id = await Task.Run(cancellationToken: token, function: () => {
                        try {
                            var directory = item?.Info as DirectoryInfo;
                            if (directory?.Exists == true) {
                                return directory.FullName;
                            }
                            return null;
                        }
                        catch {
                            return null;
                        }
                    });
                    if (id != null) {
                        worked |= await context.Provide(id, parameter.Where, token);
                    }
                }
                return worked;
            });
        }

        public sealed class Parameter {
            [CliArgument]
            public CommandContextProvide Where { get; set; }
        }
    }
}
