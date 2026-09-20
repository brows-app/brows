using Brows.Requests;

namespace Brows.Operations;

internal sealed class OperationManager {
    private void Remove(Operation operation) {
        if (null == operation) throw new ArgumentNullException(nameof(operation));
        operation.Removed -= Operation_Removed;
        Operations.Remove(operation);
    }

    private void Operation_Removed(object sender, EventArgs e) {
        if (sender is Operation operation) {
            Remove(operation);
        }
    }

    private void Operation_Completed(object sender, EventArgs e) {
        var operation = sender as Operation;
        if (operation != null) {
            operation.Completed -= Operation_Completed;
            if (operation.Relevant == false) {
                Remove(operation);
            }
            if (operation.CompleteWithError == false) {
                if (RemoveCompletedOperations) {
                    Remove(operation);
                }
            }
        }
    }

    private Operation Operation(string name, OperationDelegate task) {
        var
        operation = new Operation(name, task, RequestFactory);
        operation.Completed += Operation_Completed;
        operation.Removed += Operation_Removed;
        return operation;
    }

    public bool RemoveCompletedOperations { get; set; } = true;

    public OperationCollection Operations { get; }
    public IRequestFactory RequestFactory { get; }

    public OperationManager(OperationCollection operations, IRequestFactory requestFactory) {
        Operations = operations ?? throw new ArgumentNullException(nameof(operations));
        RequestFactory = requestFactory;
    }

    public void Operate(string name, OperationDelegate task) {
        var
        operation = Operation(name, task);
        Operations.Add(operation);
    }
}
