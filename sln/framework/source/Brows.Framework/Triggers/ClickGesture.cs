using System;

namespace Brows.Triggers;

public readonly record struct ClickGesture : IGesture, IEquatable<ClickGesture> {
    public readonly int Clicks { get; }
    public readonly ClickButton Button { get; }
    public readonly ClickModifiers Modifiers { get; }

    public ClickGesture(ClickButton button, ClickModifiers modifiers, int clicks) {
        Clicks = clicks;
        Button = button;
        Modifiers = modifiers;
    }

    public ClickGesture(ClickButton button, int count) : this(button, ClickModifiers.None, count) {
    }

    public bool Equals(IGesture other) {
        return other is ClickGesture cg && Equals(cg);
    }
}
