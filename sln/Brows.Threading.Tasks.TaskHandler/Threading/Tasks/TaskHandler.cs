using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Threading.Tasks {
    public class TaskHandler : TaskError {
        private int Task {
            get => _Task;
            set => Change(ref _Task, value, nameof(Task), nameof(Idle));
        }
        private int _Task;

        private async Task Run(Task task, bool ifIdle = false) {
            if (ifIdle && !Idle) return;
            if (task != null) {
                Task++;
                try {
                    await task;
                }
                catch (Exception ex) {
                    var canceled = ex as OperationCanceledException;
                    if (canceled != null) {
                        Canceled(canceled);
                    }
                    else {
                        Errored(ex);
                    }
                }
                finally {
                    Task--;
                }
            }
        }

        protected override string LogHeader =>
            $"{nameof(Owner)} > {Owner.Name}";

        public bool Idle =>
            Task == 0;

        public Type Owner { get; }

        public TaskHandler(Type owner) {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public async void Begin(Task task) {
            await Run(task);
        }

        public async void Begin(Func<Task> task) {
            if (task != null) {
                await Run(task());
            }
        }

        public async void Begin(Func<CancellationToken, Task> task) {
            if (task != null) {
                await Run(task(CancellationToken.None));
            }
        }
    }

    public class TaskHandler<T> : TaskHandler {
        public TaskHandler() : base(typeof(T)) {
        }
    }
}
