using System.Collections.Generic;

namespace Brows.Triggers;

/// <summary>
/// Provides gesture triggers indexed by their gestures.
/// </summary>
public interface IGestureTriggerCollection : IReadOnlyCollection<IGestureTrigger> {
    /// <summary>
    /// Gets the trigger for a gesture.
    /// </summary>
    /// <param name="gesture">
    /// The gesture to look up.
    /// </param>
    /// <returns>
    /// The trigger associated with the gesture.
    /// </returns>
    IGestureTrigger this[IGesture gesture] { get; }
}
