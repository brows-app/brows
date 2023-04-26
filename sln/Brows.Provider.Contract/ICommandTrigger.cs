namespace Brows {
    public interface ICommandTrigger {
        IInputTrigger Input { get; }
        IGestureTriggerCollection Gesture { get; }
    }
}
