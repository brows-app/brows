using System;

namespace Brows.Triggers;

/// <summary>
/// Describes a mouse button, click count, and modifier keys.
/// </summary>
public readonly record struct ClickGesture : IGesture, IEquatable<ClickGesture> {
    /// <summary>
    /// Gets the number of clicks in the gesture.
    /// </summary>
    public readonly int Clicks { get; }

    /// <summary>
    /// Gets the mouse button used by the gesture.
    /// </summary>
    public readonly ClickButton Button { get; }

    /// <summary>
    /// Gets the modifier keys held during the gesture.
    /// </summary>
    public readonly ClickModifiers Modifiers { get; }

    /// <summary>
    /// Initializes a click gesture.
    /// </summary>
    /// <param name="button">
    /// The mouse button.
    /// </param>
    /// <param name="modifiers">
    /// The modifier keys held during the clicks.
    /// </param>
    /// <param name="clicks">
    /// The number of clicks.
    /// </param>
    public ClickGesture(ClickButton button, ClickModifiers modifiers, int clicks) {
        Clicks = clicks;
        Button = button;
        Modifiers = modifiers;
    }

    /// <summary>
    /// Initializes a click gesture without modifier keys.
    /// </summary>
    /// <param name="button">
    /// The mouse button.
    /// </param>
    /// <param name="count">
    /// The number of clicks.
    /// </param>
    public ClickGesture(ClickButton button, int count) : this(button, ClickModifiers.None, count) {
    }

    /// <summary>
    /// Determines whether another gesture is a click gesture with the same button, modifiers, and click count.
    /// </summary>
    /// <param name="other">
    /// The gesture to compare.
    /// </param>
    /// <returns>
    /// True if the gestures are equal; otherwise, false.
    /// </returns>
    public bool Equals(IGesture other) {
        return other is ClickGesture cg && Equals(cg);
    }
}
