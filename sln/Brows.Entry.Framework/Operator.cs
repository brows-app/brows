using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public sealed class Operator {
        public IOperationCollection Operations =>
            _Operations ?? (
            _Operations = new OperationCollection());
        private IOperationCollection _Operations;

        public void Operate(string name, Func<IOperationProgress, CancellationToken, Task> task) {
            var manager = new OperationManager(Operations);
            var operable = manager.Operable(name);
            operable.Operate(task);
        }
    }
}
