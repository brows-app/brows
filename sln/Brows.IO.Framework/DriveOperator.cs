using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal class DriveOperator : Operator {
        protected override Task ProtectedDeploy(CancellationToken cancellationToken) {
            return Task.CompletedTask;
        }

        public DriveOperator(IOperatorDeployment deployment) : base(deployment) {
        }
    }
}
