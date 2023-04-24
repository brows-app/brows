using Domore.Conf.Cli;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class Open : DriveCommand<Open.Parameter> {
        protected override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) return false;
            if (context.GetParameter(out var parameter) == false) return false;
            if (active.HasDriveSelection(out var selection) == false) {
                return false;
            }
            return context.Operate(async (progress, token) => {
                var worked = false;
                foreach (var info in selection) {
                    var id = await Task.Run(cancellationToken: token, function: () => {
                        try {
                            var directory = info.RootDirectory;
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
