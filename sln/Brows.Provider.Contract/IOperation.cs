namespace Brows {
    public interface IOperation {
        bool Complete { get; }
        bool CompleteWithError { get; }
    }
}
