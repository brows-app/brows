using System;

namespace Brows.Triggers;

public readonly record struct PressGesture : IGesture, IEquatable<PressGesture> {
    public readonly PressKey Key { get; }
    public readonly PressModifiers Modifiers { get; }

    public PressGesture(PressKey key, PressModifiers modifiers) {
        Key = key;
        Modifiers = modifiers;
    }

    public PressGesture(PressKey key) : this(key, PressModifiers.None) {
    }

    public bool Equals(IGesture other) {
        return other is PressGesture pg && Equals(pg);
    }
}
