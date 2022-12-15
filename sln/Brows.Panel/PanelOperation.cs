using System;
using System.Threading;
using System.Threading.Tasks;
using TASK = System.Threading.Tasks.Task;

namespace Brows {
    internal class PanelOperation : IEntryOperation {
        public string Name { get; set; }

        public Func<IOperationProgress, CancellationToken, Task> Task {
            get => _Task ?? (_Task = async (_, _) => await TASK.CompletedTask);
            set => _Task = value;
        }
        private Func<IOperationProgress, CancellationToken, Task> _Task;
    }
}
