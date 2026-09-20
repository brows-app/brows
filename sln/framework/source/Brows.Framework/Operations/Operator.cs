using Brows.Requests;

namespace Brows.Operations;

public sealed class Operator {
    private readonly OperationCollection OperationCollection = new();

    public IOperationCollection Operations =>
        OperationCollection;

    public IRequestFactory RequestFactory { get; }

    public Operator(IRequestFactory requestFactory) {
        RequestFactory = requestFactory;
    }

    public void Operate(string name, OperationDelegate task) {
        var
        manager = new OperationManager(OperationCollection, RequestFactory);
        manager.Operate(name, task);
    }
}
