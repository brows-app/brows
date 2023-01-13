namespace Brows {
    public interface ICommandLine {
        bool HasInput(out string input);
        bool HasCommand(out string command);
        bool HasTrigger(out string trigger);
        bool HasParameter(out string parameter);
    }
}
