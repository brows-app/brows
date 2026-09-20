using Domore.Conf;
using Domore.Conf.Cli;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.CLI;

internal sealed class CliCommandContext : ICliCommandContext {
    private int CommandSepIdx { get; }

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called implicitly")]
    [ImportsPopulatedCallback]
    private void ImportsPopulated() {
        var commands = Commands;
        if (commands is not null) {
            foreach (var command in commands) {
                if (command is CliCommand cliCommand) {
                    /*
                     * Most or all CLI command implementations should extend this base type,
                     * so the Cli property can be populated. This is a side-effect and is
                     * kind-of hacky, but it works for now.
                     */
                    cliCommand.Cli = Cli;
                }
            }
        }
    }

    public ICliCommand Command => field ??=
        /*
         * Find the command with a name that matches the one we're looking for.
         * This is the command that will be run to execute the work of the CLI 
         * program.
         */
        Commands?.FirstOrDefault(command => true == command
            ?.Name
            ?.Equals(CommandName, StringComparison.OrdinalIgnoreCase)) ??
        /*
         * No command with the specified name was found, so we obviously can't
         * run the command.
         */
        throw new CliCommandNotFoundException(CommandName);

    public IReadOnlyList<ICliCommand> Commands { get; set; }

    public string ProgramName { get; }
    public string CommandName { get; }
    public string CommandArgs { get; }
    public string CommandLine { get; }
    public ICliCommandParam CommandParam { get; }
    public CliProvider Cli { get; }

    public CliCommandContext(CliProvider cli, string programName, string commandLine) {
        Cli = cli ?? throw new ArgumentNullException(nameof(cli));
        ProgramName = programName;
        CommandLine = commandLine ?? throw new ArgumentNullException(nameof(commandLine));
        CommandSepIdx = CommandLine.IndexOf(' ');
        CommandName = CommandSepIdx > 0
            ? CommandLine.Substring(0, CommandSepIdx)
            : CommandLine;
        CommandArgs = CommandSepIdx == -1 ? null :
            CommandSepIdx + 1 < CommandLine.Length
                ? CommandLine.Substring(CommandSepIdx + 1)
                : null;
    }

    public CliCommandContext(CliProvider cli, string programName, ICliCommandParam commandParam)
    : this(cli, programName, commandParam?.ToCommandLine()) {
        CommandParam = commandParam;
    }

    public T Configure<T>(T target) {
        /*
         * Here, we populate the target with any dependencies from the composition (DI) framework,
         * then configure the target based on the configuration of the program itself (e.g. the
         * configuration in a .conf file), then finally configure the target from the CLI command
         * itself.
         * 
         * The order of configuration is important, as it allows the CLI command to override anything
         * that's found in a .conf file, and the .conf file to override properties imported via the
         * dependency injector.
         */
        Imports.Current.Populate(target);
        Conf.Configure(target, key: "");
        Conf.Configure(target, key: Command.Name);
        Cli.Configure(target, CommandArgs);
        return target;
    }

    public async Task<T> Configure<T>(T target, CancellationToken token) {
        if (token.IsCancellationRequested) {
            token.ThrowIfCancellationRequested();
        }
        await Task.CompletedTask; // This is here for future-proofing, in case
        return Configure(target); // we need async configuration in the future.
    }

    public Task<object> Complete(CancellationToken token) {
        var command = Command;
        return command.Run(this, token);
    }
}
