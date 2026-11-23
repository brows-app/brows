namespace Brows.Programs;

/// <summary>
/// Provides startup arguments and configuration for a program.
/// </summary>
public interface IProgramContext {
    /// <summary>
    /// Gets the program's startup command.
    /// </summary>
    IProgramCommand Command { get; }

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
