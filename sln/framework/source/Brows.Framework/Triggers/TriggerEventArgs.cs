namespace Brows.Triggers;

/// <summary>
/// Provides the source and handled state of input that may trigger a command.
/// </summary>
public abstract class TriggerEventArgs : EventArgs {
    /// <summary>
    /// Initializes an event for input from a source.
    /// </summary>
    /// <param name="source">
    /// The object from which the input originated.
    /// </param>
    protected TriggerEventArgs(object source) {
        Source = source;
    }

    /// <summary>
    /// Gets or sets whether the input has triggered a command.
    /// </summary>
    public bool Triggered { get; set; }

    /// <summary>
    /// Gets the object from which the input originated.
    /// </summary>
    public object Source { get; }
}
