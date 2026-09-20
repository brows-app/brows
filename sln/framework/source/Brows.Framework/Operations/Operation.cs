using Brows.Requests;
using System;

namespace Brows.Operations;

internal sealed class Operation : OperationBase {
    private void Remove() {
        Removed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler Removed;

    public object CancelCommand => field ??= Request?.Create(
        owner: this,
        execute: _ => Cancel(),
        canExecute: _ => Progressing && !Canceling,
        dependencies: [nameof(Progressing), nameof(Canceling)]);

    public object RemoveCommand => field ??= Request?.Create(
        owner: this,
        execute: _ => Remove(),
        canExecute: _ => !Progressing,
        dependencies: [nameof(Progressing)]);


    public IRequestFactory Request { get; }

    public Operation(string name, OperationDelegate task, IRequestFactory request) : base(name, null, task) {
        Request = request;
    }
}
