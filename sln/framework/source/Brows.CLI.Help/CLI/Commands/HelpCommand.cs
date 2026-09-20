using Domore.Conf;
using Domore.Conf.Cli;
using Domore.Logs;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.CLI.Commands;

internal sealed class HelpCommand : CliCommand<HelpCommand.Param> {
    private static readonly ILog Log = Logging.For(typeof(HelpCommand));

    new internal CliProvider Cli {
        get => base.Cli;
        set => base.Cli = value;
    }

    protected override async Task<object> Run(Param param, ICliCommandContext context, CancellationToken token) {
        if (null == param) throw new ArgumentNullException(nameof(param));
        if (null == context) throw new ArgumentNullException(nameof(context));
        var commands = context.Commands ?? [];
        if (commands == null || commands.Count == 0) {
            if (Log.Critical()) {
                Log.Critical("No commands available");
            }
            return null;
        }
        var help = default(string);
        var commandName = param.Command?.Trim() ?? "";
        if (commandName == "") {
            help = string.Join(Environment.NewLine,
                new[] {
                    $"usage: {context.ProgramName} [command] [arguments] [parameters]",
                    $"commands:"
                }
                .Concat(commands.Select(c => "    " + c.Help)));
        }
        else {
            var command = commands.FirstOrDefault(c => commandName.Equals(c.Name, StringComparison.OrdinalIgnoreCase));
            if (command == null) {
                if (Log.Warn()) {
                    Log.Warn($"{commandName} not found");
                }
                return null;
            }
            help = command.Manual;
        }
        if (Log.Info()) {
            Log.Info(Out($"{help}"));
        }
        await Task.CompletedTask;
        return help;
    }

    [ConfHelp(@"
        Displays general or command-specific help.")]
    public sealed class Param {
        [CliArgument]
        [ConfHelp(@"
            The name of a command for command-specific help.")]
        public string Command { get; set; }
    }
}
