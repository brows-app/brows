namespace Brows {
    public interface ICommandTrigger {
        IInputTriggerCollection Inputs { get; }
        IGestureTriggerCollection Gestures { get; }
        bool Triggered(string s, out IInputTrigger trigger);
        bool Triggered(IGesture g, out IGestureTrigger trigger);
    }
}
