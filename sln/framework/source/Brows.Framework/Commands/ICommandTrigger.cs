using Brows.Triggers;
using System.Collections.Generic;

namespace Brows.Commands;

/// <summary>
/// Provides configured input and gesture triggers for a command.
/// </summary>
public interface ICommandTrigger {
    /// <summary>
    /// Gets the command's text input triggers.
    /// </summary>
    IInputTriggerCollection Inputs { get; }

    /// <summary>
    /// Gets the command's gesture triggers.
    /// </summary>
    IGestureTriggerCollection Gestures { get; }

    /// <summary>
    /// Tries to find an input trigger matching the supplied text.
    /// </summary>
    /// <param name="s">
    /// The input to test.
    /// </param>
    /// <param name="trigger">
    /// Receives the matching input trigger.
    /// </param>
    /// <returns>
    /// True if a trigger matches; otherwise, false.
    /// </returns>
    bool Triggered(string s, out IInputTrigger trigger);

    /// <summary>
    /// Tries to find a trigger matching the supplied gesture.
    /// </summary>
    /// <param name="g">
    /// The gesture to test.
    /// </param>
    /// <param name="trigger">
    /// Receives the matching gesture trigger.
    /// </param>
    /// <returns>
    /// True if a trigger matches; otherwise, false.
    /// </returns>
    bool Triggered(IGesture g, out IGestureTrigger trigger);

    /// <summary>
    /// Enumerates the configured keyboard triggers.
    /// </summary>
    /// <returns>
    /// The keyboard gesture triggers.
    /// </returns>
    IEnumerable<IGestureTrigger> PressGestures() {
        var gestures = Gestures;
        if (gestures is null) {
            yield break;
        }
        foreach (var gesture in gestures) {
            if (gesture?.Gesture is PressGesture) {
                yield return gesture;
            }
        }
        yield break;
    }

    /// <summary>
    /// Enumerates the configured mouse triggers.
    /// </summary>
    /// <returns>
    /// The mouse gesture triggers.
    /// </returns>
    IEnumerable<IGestureTrigger> ClickGestures() {
        var gestures = Gestures;
        if (gestures is null) {
            yield break;
        }
        foreach (var gesture in gestures) {
            if (gesture?.Gesture is ClickGesture) {
                yield return gesture;
            }
        }
        yield break;
    }
}
