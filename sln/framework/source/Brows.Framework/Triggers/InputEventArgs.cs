namespace Brows.Triggers;

/// <summary>
/// Describes text input and whether it has triggered a command.
/// </summary>
public sealed class InputEventArgs : TriggerEventArgs {
    /// <summary>
    /// Gets the text that raised the event.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Initializes an event for text input from a source.
    /// </summary>
    /// <param name="text">
    /// The input text.
    /// </param>
    /// <param name="source">
    /// The object from which the input originated.
    /// </param>
    public InputEventArgs(string text, object source) : base(source) {
        Text = text;
    }
}
