using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Theme : Command<Theme.Parameter>, ICommandExport {
        protected override async Task<bool> Work(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasCommander(out var commander)) {
                if (context.HasParameter(out var parameter)) {
                    await commander.SetTheme(
                        @base: parameter.Base,
                        background: parameter.Background,
                        foreground: parameter.Foreground,
                        cancellationToken);
                    return true;
                }
            }
            return false;
        }

        public class Parameter {
            [Argument(Name = "base")]
            public string Base { get; set; }

            [Switch(Name = "background", ShortName = 'b')]
            public string Background { get; set; }

            [Switch(Name = "foreground", ShortName = 'f')]
            public string Foreground { get; set; }
        }
    }
}
