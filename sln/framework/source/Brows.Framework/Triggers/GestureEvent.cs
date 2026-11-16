namespace Brows.Triggers;

public delegate void GestureEventHandler(object sender, GestureEventArgs e);

public sealed class GestureEventArgs : TriggerEventArgs {
    public IGesture Gesture { get; }

    public GestureEventArgs(IGesture gesture, object source) : base(source) {
        Gesture = gesture;
    }
}
