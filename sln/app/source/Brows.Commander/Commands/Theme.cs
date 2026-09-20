using Brows.Config;
using Brows.Gui;
using Domore.Conf;
using Domore.Conf.Cli;
using System.Threading.Tasks;

namespace Brows.Commands;

internal sealed class Theme : Command<Theme.Parameter> {
    private IConfig<CommanderTheme> Data => field ??=
        Configure.Data<CommanderTheme>();

    [ImportRequired]
    internal ICommanderTheme CommanderTheme { get; set; }

    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (!context.HasCommander(out var commander) ||
            !context.HasParameter(out var parameter)) {
            return false;
        }
        return context.Operate(async (progress, token) => {
            var
            theme = await Data.Load(token);
            theme.Base = parameter.Base;
            theme.Background = parameter.Background;
            theme.Foreground = parameter.Foreground;
            CommanderTheme.Apply(theme);
            return true;
        });
    }

    protected sealed override async Task Init(CancellationToken token) {
        var theme = await Data.Load(token);
        CommanderTheme.Apply(theme);
    }

    public sealed class Parameter {
        [CliArgument]
        [CliRequired]
        public string Base { get; set; }

        [Conf("bg")]
        public string Background { get; set; }

        [Conf("fg")]
        public string Foreground { get; set; }
    }
}
