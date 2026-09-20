namespace Brows.Programs;

public interface IProgramContext {
    IProgramCommand Command { get; }
    T Configure<T>(T target);
}
