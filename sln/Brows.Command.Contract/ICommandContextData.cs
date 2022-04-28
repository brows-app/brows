namespace Brows {
    public interface ICommandContextData {
        string Input { get; }
        object Current { get; }
        ICommand Command { get; }
        ICommandContextFlag Flag { get; }
        ICommandContextData Remove();
        ICommandContextData Clear();
        ICommandContextData Next();
        ICommandContextData Previous();
        ICommandContextData Enter();
    }
}
