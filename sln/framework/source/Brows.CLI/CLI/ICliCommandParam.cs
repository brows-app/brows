namespace Brows.CLI;

/// <summary>
/// A parameter that may be passed to a CLI program.
/// </summary>
public interface ICliCommandParam {
    /// <summary>
    /// Creates a command-line representation of the parameter.
    /// </summary>
    /// <returns>
    /// The command line for the parameter.
    /// </returns>
    string ToCommandLine();
}
