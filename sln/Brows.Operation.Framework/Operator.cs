using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class Operator : IOperator {
        private OperationManager Manager =>
            _Manager ?? (
            _Manager = new OperationManager(Operations, Dialog) { RemoveCompletedOperations = Deployment.RemoveCompletedOperations });
        private OperationManager _Manager;

        protected IPayload Payload => Deployment.Payload;
        protected IPayloadTarget Target => Deployment.Target;
        protected IDialogState Dialog => Deployment.Dialog;
        protected IOperationCollection Operations => Deployment.Operations;

        protected virtual async ValueTask DisposeAsyncCore() {
            await Manager.DisposeAsync().ConfigureAwait(false);
        }

        protected IOperationProgress Operation(CancellationToken cancellationToken, string name) {
            return Manager.Progress(cancellationToken, name);
        }

        protected abstract Task ProtectedDeploy(CancellationToken cancellationToken);

        public IOperatorDeployment Deployment { get; }

        public Operator(IOperatorDeployment deployment) {
            Deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));
        }

        public async Task Deploy(CancellationToken cancellationToken) {
            await ProtectedDeploy(cancellationToken);
        }

        public async ValueTask DisposeAsync() {
            await DisposeAsyncCore().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}
