using System.Collections.Generic;

namespace Brows.CLI;

/// <summary>
/// The context of a running command.
/// </summary>
public interface ICliCommandContext {
    /// <summary>
    /// Gets the name of the program in which the command is running.
    /// </summary>
    string ProgramName { get; }

    /// <summary>
    /// Gets the currently running command.
    /// </summary>
    ICliCommand Command { get; }

    /// <summary>
    /// Gets a collection of the commands available in the context.
    /// </summary>
    IReadOnlyList<ICliCommand> Commands { get; }
}
