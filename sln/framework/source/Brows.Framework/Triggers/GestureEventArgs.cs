namespace Brows.Triggers;

public sealed class GestureEventArgs : TriggerEventArgs {
    public IGesture Gesture { get; }

    public GestureEventArgs(IGesture gesture, object source) : base(source) {
        Gesture = gesture;
    }
}
