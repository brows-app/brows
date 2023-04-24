using System;

namespace Brows {
    public class OperationManager : IOperationManager {
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
                if (operation.CompletedWithError == false) {
                    if (RemoveCompletedOperations) {
                        Remove(operation);
                    }
                }
            }
        }

        private Operation Operation(string name) {
            var
            operation = new Operation(name);
            operation.Completed += Operation_Completed;
            operation.Removed += Operation_Removed;
            Operations.Add(operation);
            return operation;
        }

        public bool RemoveCompletedOperations { get; set; } = true;

        public IOperationCollection Operations { get; }

        public OperationManager(IOperationCollection operations) {
            Operations = operations ?? throw new ArgumentNullException(nameof(operations));
        }

        public IOperable Operable(string name) {
            return Operation(name);
        }
    }
}
