namespace Brows {
    public interface ICommandTrigger {
        ITriggerInput Input { get; }
        ITriggerPressCollection Press { get; }
    }
}
