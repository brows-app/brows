namespace Brows {
    public interface IProgramContext {
        IProgramCommand Command { get; }
        IProgramComposition Composition { get; }
    }
}
