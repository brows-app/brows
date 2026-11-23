namespace Brows.Programs;

/// <summary>
/// Provides a program context backed by a startup command.
/// </summary>
public class ProgramContext : IProgramContext {
    /// <summary>
    /// Gets the startup command used by this context.
    /// </summary>
    public IProgramCommand Command { get; }

    /// <summary>
    /// Initializes a context for a startup command.
    /// </summary>
    /// <param name="command">
    /// The command supplying startup arguments and configuration.
    /// </param>
    public ProgramContext(IProgramCommand command) {
        Command = command ?? throw new ArgumentNullException(nameof(command));
    }

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
    public T Configure<T>(T target) {
        return Command.Configure(target);
    }
}
