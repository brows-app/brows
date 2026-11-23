namespace Brows.Triggers;

/// <summary>
/// Handles a gesture that may trigger a command.
/// </summary>
/// <param name="sender">
/// The object that raised the event.
/// </param>
/// <param name="e">
/// The event data.
/// </param>
public delegate void GestureEventHandler(object sender, GestureEventArgs e);

/// <summary>
/// Describes a gesture and whether it has triggered a command.
/// </summary>
public sealed class GestureEventArgs : TriggerEventArgs {
    /// <summary>
    /// Gets the gesture that raised the event.
    /// </summary>
    public IGesture Gesture { get; }

    /// <summary>
    /// Initializes an event for a gesture from a source.
    /// </summary>
    /// <param name="gesture">
    /// The gesture that raised the event.
    /// </param>
    /// <param name="source">
    /// The object from which the gesture originated.
    /// </param>
    public GestureEventArgs(IGesture gesture, object source) : base(source) {
        Gesture = gesture;
    }
}
