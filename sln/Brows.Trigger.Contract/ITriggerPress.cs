namespace Brows {
    public interface ITriggerPress : ITrigger {
        PressKey Key { get; }
        PressModifiers Modifiers { get; }
        PressGesture Gesture { get; }
        string Shortcut { get; }
    }
}
