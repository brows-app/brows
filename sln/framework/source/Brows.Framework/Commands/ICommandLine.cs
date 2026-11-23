namespace Brows.Commands;

/// <summary>
/// Provides the command name, arguments, and configuration from a line of input.
/// </summary>
public interface ICommandLine {
    /// <summary>
    /// Tries to get the full input.
    /// </summary>
    /// <param name="input">
    /// Receives the full input.
    /// </param>
    /// <returns>
    /// True if the requested text is present; otherwise, false.
    /// </returns>
    bool HasInput(out string input);

    /// <summary>
    /// Tries to get the command name.
    /// </summary>
    /// <param name="command">
    /// Receives the command name.
    /// </param>
    /// <returns>
    /// True if the requested text is present; otherwise, false.
    /// </returns>
    bool HasCommand(out string command);

    /// <summary>
    /// Tries to get the confirmed command trigger.
    /// </summary>
    /// <param name="trigger">
    /// Receives the confirmed command trigger.
    /// </param>
    /// <returns>
    /// True if a space follows the command name; otherwise, false.
    /// </returns>
    bool HasTrigger(out string trigger);

    /// <summary>
    /// Tries to get the command arguments.
    /// </summary>
    /// <param name="parameter">
    /// Receives the command arguments.
    /// </param>
    /// <returns>
    /// True if the requested text is present; otherwise, false.
    /// </returns>
    bool HasParameter(out string parameter);

    /// <summary>
    /// Tries to get the configuration text.
    /// </summary>
    /// <param name="conf">
    /// Receives the configuration text.
    /// </param>
    /// <returns>
    /// True if the requested text is present; otherwise, false.
    /// </returns>
    bool HasConf(out string conf);
}
