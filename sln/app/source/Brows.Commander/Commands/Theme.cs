using Brows.Conf;
using Brows.Gui;
using Domore.Conf.Cli;
using Domore.Logs;
using System.Threading.Tasks;

namespace Brows.Commands;

internal sealed class Theme : Command<Theme.Parameter> {
    private static readonly ILog Log = Logging.For(typeof(Theme));

    private IConfiguration<CommanderTheme> Data => field ??=
        ConfigData.Data<CommanderTheme>("theme");

    [ImportRequired]
    internal ICommanderTheme CommanderTheme { get; set; }

    [ImportRequired]
    internal IConfigurationData ConfigData { get; set; }

    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (!context.HasCommander(out _)) {
            return false;
        }
        if (!context.HasParameter(out var parameter)) {
            return false;
        }
        return context.Operate(async (progress, token) => {
            var theme = await Data.Load(token);
            CommanderTheme.Apply(new CommanderTheme { Name = parameter.Name, Mode = parameter.Mode });
            theme.Name = parameter.Name;
            theme.Mode = parameter.Mode;
            return true;
        });
    }

    protected sealed override async Task Init(CancellationToken token) {
        var theme = await Data.Load(token);
        try {
            CommanderTheme.Apply(theme);
        }
        catch (ArgumentException ex) {
            if (Log.Warn()) {
                Log.Warn(Log.Join(nameof(Init), "Invalid configured theme; using FileExplorer dark."), ex);
            }
            CommanderTheme.Apply(new CommanderTheme {
                Name = "FileExplorer",
                Mode = CommanderThemeMode.Dark
            });
        }
    }

    public sealed record Parameter {
        [CliArgument]
        [CliRequired]
        public string Name { get; init; }

        [CliArgument]
        [CliRequired]
        public CommanderThemeMode Mode { get; init; }
    }
}
