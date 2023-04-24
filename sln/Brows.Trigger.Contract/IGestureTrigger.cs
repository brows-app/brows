namespace Brows {
    public interface IGestureTrigger {
        IGesture Gesture { get; }
        string Shortcut { get; }
        string Display { get; }
    }
}
