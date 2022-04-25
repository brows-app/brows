namespace Brows {
    public interface ICommandContextData {
        string Input { get; }
        string Control { get; }
        object Current { get; }
        object KeyTarget { get; }
        ICommand Command { get; }
        ICommandContextData Remove();
        ICommandContextData RemoveAll();
        ICommandContextData Next();
        ICommandContextData Previous();
        void Enter();
    }
}
