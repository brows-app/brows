using System;

namespace Brows.Triggers;

/// <summary>
/// Describes a key press and its modifier keys.
/// </summary>
public readonly record struct PressGesture : IGesture, IEquatable<PressGesture> {
    /// <summary>
    /// Gets the key used by the gesture.
    /// </summary>
    public readonly PressKey Key { get; }

    /// <summary>
    /// Gets the modifier keys held during the gesture.
    /// </summary>
    public readonly PressModifiers Modifiers { get; }

    /// <summary>
    /// Initializes a key-press gesture.
    /// </summary>
    /// <param name="key">
    /// The pressed key.
    /// </param>
    /// <param name="modifiers">
    /// The modifier keys held during the press.
    /// </param>
    public PressGesture(PressKey key, PressModifiers modifiers) {
        Key = key;
        Modifiers = modifiers;
    }

    /// <summary>
    /// Initializes a key-press gesture without modifier keys.
    /// </summary>
    /// <param name="key">
    /// The pressed key.
    /// </param>
    public PressGesture(PressKey key) : this(key, PressModifiers.None) {
    }

    /// <summary>
    /// Determines whether another gesture is a key-press gesture with the same key and modifiers.
    /// </summary>
    /// <param name="other">
    /// The gesture to compare.
    /// </param>
    /// <returns>
    /// True if the gestures are equal; otherwise, false.
    /// </returns>
    public bool Equals(IGesture other) {
        return other is PressGesture pg && Equals(pg);
    }
}
