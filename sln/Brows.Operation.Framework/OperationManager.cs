using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public class OperationManager : IOperationManager {
        private readonly List<Operation> Creations = new List<Operation>();

        private void Remove(Operation operation) {
            if (null == operation) throw new ArgumentNullException(nameof(operation));
            operation.Removed -= Operation_Removed;
            var operations = Operations;
            if (operations != null) {
                operations.Remove(operation);
            }
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
            var operation = new Operation(name);
            var operations = Operations;
            if (operations != null) {
                operations.Add(operation);
            }
            operation.Dialog = Dialog;
            operation.Completed += Operation_Completed;
            operation.Removed += Operation_Removed;
            Creations.Add(operation);
            return operation;
        }

        protected virtual async ValueTask DisposeAsyncCore() {
            foreach (var operation in Creations) {
                operation.End();
            }
            await Task.CompletedTask;
        }

        public bool RemoveCompletedOperations { get; set; } = true;

        public IDialogState Dialog { get; }
        public IOperationCollection Operations { get; }

        public OperationManager(IOperationCollection operations, IDialogState dialog) {
            Operations = operations ?? throw new ArgumentNullException(nameof(operations));
            Dialog = dialog;
        }

        public IOperable Operable(string name) {
            return Operation(name);
        }

        public IOperationProgress Progress(CancellationToken cancellationToken, string name) {
            var operation = Operation(name);
            return operation.Begin(cancellationToken);
        }

        public async ValueTask DisposeAsync() {
            await DisposeAsyncCore().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}
