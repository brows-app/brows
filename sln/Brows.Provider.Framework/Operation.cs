using System;

namespace Brows {
    internal sealed class Operation : OperationBase {
        private void Remove() {
            Removed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Removed;

        public object CancelCommand => Request.Create(
            owner: this,
            execute: _ => Cancel(),
            canExecute: _ => Progressing && !Canceling,
            dependencies: [nameof(Progressing), nameof(Canceling)]);

        public object RemoveCommand => Request.Create(
            owner: this,
            execute: _ => Remove(),
            canExecute: _ => !Progressing,
            dependencies: [nameof(Progressing)]);

        public Operation(string name, OperationDelegate task) : base(name, null, task) {
        }
    }
}
