namespace Brows.Triggers;

/// <summary>
/// Matches a user gesture to a configured command trigger.
/// </summary>
public interface IGestureTrigger : ITrigger {
    /// <summary>
    /// Gets the gesture that activates the trigger.
    /// </summary>
    IGesture Gesture { get; }

    /// <summary>
    /// Gets the text used to display the gesture.
    /// </summary>
    string Display { get; }

    /// <summary>
    /// Determines whether a gesture activates this trigger.
    /// </summary>
    /// <param name="gesture">
    /// The gesture to test.
    /// </param>
    /// <returns>
    /// True if the gesture matches; otherwise, false.
    /// </returns>
    bool Triggered(IGesture gesture);
}
