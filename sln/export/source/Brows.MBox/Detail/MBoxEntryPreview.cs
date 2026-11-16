using Brows.Gui;
using Brows.Providers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Detail;

/// <summary>
/// Shows the preview of the current message of a mailbox.
/// </summary>
internal sealed class MBoxEntryPreview : ProviderDetail, IControlled<IMBoxEntryPreviewController>, IProviderDetailFocus {
    private readonly Lock _ControllerLocker = new();
    private TaskCompletionSource<IMBoxEntryPreviewController> _ControllerChanged = NewControllerChanged();

    private IMBoxEntryPreviewController _Controller;

    /// <summary>
    /// Sets the controller of the preview.
    /// </summary>
    IMBoxEntryPreviewController IControlled<IMBoxEntryPreviewController>.Controller {
        set {
            TaskCompletionSource<IMBoxEntryPreviewController> changed;
            lock (_ControllerLocker) {
                if (ReferenceEquals(_Controller, value)) {
                    return;
                }
                _Controller = value;
                changed = _ControllerChanged;
                _ControllerChanged = NewControllerChanged();
            }
            changed.TrySetResult(value);
        }
    }

    /// <summary>
    /// Gets the controller of the preview, or <see langword="null"/> when the preview has none.
    /// </summary>
    internal IMBoxEntryPreviewController Controller {
        get {
            lock (_ControllerLocker) {
                return _Controller;
            }
        }
    }

    protected override ProviderDetail Clone(Provider provider) {
        return provider is MBoxProvider p
            ? new MBoxEntryPreview(p)
            : null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxEntryPreview"/> class.
    /// </summary>
    /// <param name="provider">
    /// The provider that shows the mailbox.
    /// </param>
    public MBoxEntryPreview(MBoxProvider provider) : base(provider) {
    }

    /// <summary>
    /// Waits for the controller of the preview to be attached.
    /// </summary>
    /// <param name="timeout">
    /// The longest time to wait.
    /// </param>
    /// <param name="token">
    /// The token that cancels the wait.
    /// </param>
    /// <returns>
    /// The controller of the preview, or <see langword="null"/> when it was not attached in
    /// time.
    /// </returns>
    internal async Task<IMBoxEntryPreviewController> WaitForController(TimeSpan timeout,
                                                                       CancellationToken token) {
        var delay = Task.Delay(timeout, token);
        while (token.IsCancellationRequested == false) {
            Task<IMBoxEntryPreviewController> changed;
            lock (_ControllerLocker) {
                if (_Controller is not null) {
                    return _Controller;
                }
                changed = _ControllerChanged.Task;
            }
            var completed = await Task.WhenAny(changed, delay);
            if (completed != changed) {
                return null;
            }
            await changed;
        }
        return null;
    }

    /// <summary>
    /// Asks the controller of the preview to take the keyboard focus.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the preview took the focus.
    /// </returns>
    internal bool RequestFocus() {
        var controller = Controller;
        var controllerIsCurrent = controller is not null && ReferenceEquals(Controller, controller);
        if (controllerIsCurrent == false) {
            return false;
        }
        return controller.RequestFocus();
    }

    private static TaskCompletionSource<IMBoxEntryPreviewController> NewControllerChanged() {
        return new TaskCompletionSource<IMBoxEntryPreviewController>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    bool IProviderDetailFocus.HasKeyboardFocus {
        get {
            var controller = Controller;
            var controllerIsCurrent = controller is not null && ReferenceEquals(Controller, controller);
            return controllerIsCurrent && controller.HasKeyboardFocus;
        }
    }

    bool IProviderDetailFocus.RequestFocus() {
        var controller = Controller;
        var controllerIsCurrent = controller is not null && ReferenceEquals(Controller, controller);
        if (controllerIsCurrent == false) {
            return false;
        }
        return controller.RequestFocus();
    }
}
