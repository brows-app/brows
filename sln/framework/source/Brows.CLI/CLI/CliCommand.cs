using Domore.Conf.Cli;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.CLI;

/// <summary>
/// The base class for CLI commands.
/// </summary>
public abstract class CliCommand : ICliCommand {
    private string TypeName => field ??= GetType().Name;

    /// <summary>
    /// When overridden in a derived class, gets help text for the command.
    /// </summary>
    private protected abstract string Help { get; }

    /// <summary>
    /// When overridden in a derived class, gets manual text for the command.
    /// </summary>
    private protected abstract string Manual { get; }

    private protected CliCommand() {
        /*
         * Implementations should extend the type that takes a generic parameter.
         * Do not extend this type directly.
         */
    }

    /// <summary>
    /// When overridden in a derived class, runs the command.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>
    /// A task that completes when the command has completed, and results in the result of the command operation.
    /// </returns>
    private protected abstract Task<object> Run(ICliCommandContext context, CancellationToken token);

    /// <summary>
    /// Gets the CLI provider of the command.
    /// </summary>
    protected internal CliProvider Cli { get; set; }

    /// <summary>
    /// Gets the name of the command.
    /// </summary>
    protected virtual string Name => field ??=
        (TypeName.EndsWith("Command", StringComparison.OrdinalIgnoreCase)
            ? TypeName.Substring(0, TypeName.Length - "Command".Length)
            : TypeName).ToLowerInvariant();

    /// <summary>
    /// Produces output related the the command.
    /// </summary>
    /// <param name="data">The data of the output.</param>
    /// <returns>Output data.</returns>
    protected static object Out(object data) {
        return new CliCommandOutput(data?.ToString() ?? "");
    }

    string ICliCommand.Name => Name;
    string ICliCommand.Help => Help;
    string ICliCommand.Manual => Manual;

    Task<object> ICliCommand.Run(ICliCommandContext context, CancellationToken token) {
        return Run(context, token);
    }
}

/// <summary>
/// The base class for CLI commands that accept parameters of a certain type.
/// </summary>
/// <typeparam name="TParam">The type of parameter accepted by the command.</typeparam>
public abstract class CliCommand<TParam> : CliCommand where TParam : new() {
    private protected sealed override string Help => field ??=
        Cli?.Display(new TParam());

    private protected sealed override string Manual => field ??=
        Cli?.Manual(new TParam());

    private protected sealed override async Task<object> Run(ICliCommandContext context, CancellationToken token) {
        var param = context is CliCommandContext c && c.CommandParam is TParam p
            ? p
            : new TParam();
        if (context is CliCommandContext commandContext) {
            param = await commandContext.Configure(param, token);
        }
        return await Run(param, context, token);
    }

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="param">The command parameter.</param>
    /// <param name="context">The command context.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>
    /// A task that completes when the command has completed, and results in the result of the command operation.
    /// </returns>
    protected abstract Task<object> Run(TParam param, ICliCommandContext context, CancellationToken token);
}
