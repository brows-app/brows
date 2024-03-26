namespace Brows {
    public interface IProgramContext {
        IProgramCommand Command { get; }
        T Configure<T>(T target);
    }
}
