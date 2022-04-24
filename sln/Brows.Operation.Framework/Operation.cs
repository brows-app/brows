using System;

namespace Brows {
    internal class Operation : OperationBase {
        private void Remove() {
            OnRemoved(EventArgs.Empty);
        }

        protected virtual void OnRemoved(EventArgs e) {
            Removed?.Invoke(this, e);
        }

        public event EventHandler Removed;

        public object CancelCommand => Request.Create(
            owner: this,
            execute: _ => { } /*Cancel()*/,
            canExecute: _ => Progressing && !Canceling,
            dependencies: new[] {
                nameof(Progressing),
                nameof(Canceling) });

        public object RemoveCommand => Request.Create(
            owner: this,
            execute: _ => Remove(),
            canExecute: _ => !Progressing,
            dependencies: new[] {
                nameof(Progressing) });

        public Operation(OperationInfo info) : base(info) {
        }
    }
}
