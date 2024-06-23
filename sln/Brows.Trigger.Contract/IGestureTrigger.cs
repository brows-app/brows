namespace Brows {
    public interface IGestureTrigger : ITrigger {
        IGesture Gesture { get; }
        string Display { get; }
        bool Triggered(IGesture gesture);
    }
}
