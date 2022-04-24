using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class Operator : IOperator {
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
                    if (Deployment.RemoveCompletedOperations) {
                        Remove(operation);
                    }
                }
            }
        }

        protected IPayload Payload => Deployment.Payload;
        protected IPayloadTarget Target => Deployment.Target;
        protected IDialogState Dialog => Deployment.Dialog;
        protected IOperationCollection Operations => Deployment.Operations;

        protected IOperationProgress Operation(CancellationToken cancellationToken, string name, string descriptionFormat, params string[] descriptionData) {
            var info = new OperationInfo(name, descriptionFormat, descriptionData);
            var operation = new Operation(info);
            var operations = Operations;
            if (operations != null) {
                operations.Add(operation);
            }
            operation.Dialog = Dialog;
            operation.Completed += Operation_Completed;
            operation.Removed += Operation_Removed;
            Creations.Add(operation);
            return operation.Begin(cancellationToken);
        }

        protected abstract Task ProtectedDeploy(CancellationToken cancellationToken);

        public IOperatorDeployment Deployment { get; }

        public Operator(IOperatorDeployment deployment) {
            Deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));
        }

        public async Task Deploy(CancellationToken cancellationToken) {
            await ProtectedDeploy(cancellationToken);
            foreach (var operation in Creations) {
                operation.End();
            }
        }
    }
}
