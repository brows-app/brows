namespace Brows.Operations;

public interface IOperation {
    bool Complete { get; }
    bool CompleteWithError { get; }
}
