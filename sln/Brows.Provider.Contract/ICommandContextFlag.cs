namespace Brows {
    public interface ICommandContextFlag {
        bool PersistInput { get; }
        bool RefreshInput { get; }
        int SelectInputStart { get; }
        int SelectInputLength { get; }
        string SetInput { get; }
    }
}
