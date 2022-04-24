namespace Brows {
    public interface ICommandContextData {
        string Input { get; }
        bool CanNextPrevious { get; }
        bool CanUpDown { get; }
        bool CanRemove { get; }
        string Control { get; }
        object Current { get; }
        ICommand Command { get; }
        ICommandContextData Remove();
        ICommandContextData RemoveAll();
        ICommandContextData Next();
        ICommandContextData Previous();
        void Up();
        void Down();
        void PageUp();
        void PageDown();
        void Enter();
    }
}
