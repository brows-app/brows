using System.Collections.Generic;

namespace Brows.Programs;

/// <summary>
/// Provides a program's command line and applies its arguments to configuration.
/// </summary>
public interface IProgramCommand {
    /// <summary>
    /// Gets the command line used to start the program.
    /// </summary>
    string CommandLine { get; }

    /// <summary>
    /// Gets the program's command-line arguments.
    /// </summary>
    IReadOnlyList<string> Args { get; }

    /// <summary>
    /// Applies startup arguments to a configuration object.
    /// </summary>
    /// <typeparam name="T">
    /// The configuration type.
    /// </typeparam>
    /// <param name="target">
    /// The object to configure.
    /// </param>
    /// <returns>
    /// The configured object.
    /// </returns>
    T Configure<T>(T target);
}
