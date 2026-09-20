using Brows.CLI;
using Domore.Conf;
using Domore.Conf.Cli;
using System.Linq;

namespace Brows.IPC;

/// <summary>
/// Base class for command parameters.
/// </summary>
public abstract class HostCommandParam : ICliCommandParam {
    /// <summary>
    /// When overridden in a derived class, gets the name of the command.
    /// </summary>
    /// <returns>The name of the command.</returns>
    protected abstract string GetCommandName();

    /// <summary>
    /// When overridden in a derived class, gets the parts of a command line.
    /// </summary>
    /// <returns>The parts of a command line.</returns>
    protected abstract string[] GetCommandParts();

    /// <summary>
    /// Gets or sets the common parameters for a command.
    /// </summary>
    [Conf(ignore: true)]
    [CliDisplay(include: false)]
    public HostProcessParam Common { get; set; }

    /// <summary>
    /// Creates a command line for the parameter.
    /// </summary>
    /// <returns>The command line for the parameter.</returns>
    public string ToCommandLine() {
        var commandName = GetCommandName()?.ToLowerInvariant();
        var commandParts = GetCommandParts() ?? [];
        var commandLine = string.Join(" ",
            new[] { commandName }
                .Concat(commandParts)
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        return commandLine;
    }

    /// <summary>
    /// Returns a string representation of the current object in the form of a command-line argument.
    /// </summary>
    /// <remarks>
    /// The returned string is formatted to represent the object as it would appear in a command-line
    /// context.
    /// </remarks>
    /// <returns>A string that represents the current object as a command-line argument.</returns>
    public sealed override string ToString() {
        return ToCommandLine();
    }
}
