using Brows.Commands;
using Brows.Gui;
using Brows.Panels;
using Brows.Providers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Detail;

internal sealed class FileSystemTerminal : ProviderDetail,
                                           IControlled<IFileSystemTerminalController>,
                                           IControllingCommandGesture,
                                           IControllingText,
                                           IProviderDetailFocus,
                                           IProviderDetailLifetime {
    private sealed record PendingControllerCleanup(IFileSystemTerminalController Controller, Task Task);

    private readonly Lock ControllerLocker = new();
    /*
     * A view can detach before its native stop completes. Keep that completion
     * with the model so provider shutdown still joins cleanup of older hosts.
     */
    private readonly List<PendingControllerCleanup> PendingCleanups = [];
    private TaskCompletionSource<IFileSystemTerminalController> ControllerChanged = NewControllerChanged();
    private IFileSystemTerminalController ControllerValue;
    private readonly CancellationTokenSource Lifetime;
    private readonly CancellationToken LifetimeTokenValue;
    private long RequestGenerationValue;
    private bool Invalidated;
    private bool LifetimeDisposed;
    private Task CloseTask;
    private Task CleanupTask;

    private static TaskCompletionSource<IFileSystemTerminalController> NewControllerChanged() {
        return new TaskCompletionSource<IFileSystemTerminalController>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void SetController(IFileSystemTerminalController value) {
        TaskCompletionSource<IFileSystemTerminalController> changed;
        lock (ControllerLocker) {
            var controllerCannotBeSet = Invalidated || ReferenceEquals(ControllerValue, value);
            if (controllerCannotBeSet) {
                return;
            }
            ControllerValue = value;
            changed = ControllerChanged;
            ControllerChanged = NewControllerChanged();
        }
        changed.TrySetResult(value);
    }

    private void DisposeLifetime() {
        CancellationTokenSource lifetime;
        lock (ControllerLocker) {
            if (LifetimeDisposed) {
                return;
            }
            LifetimeDisposed = true;
            lifetime = Lifetime;
        }
        lifetime.Dispose();
    }

    private async Task CloseCore() {
        ((IProviderDetailLifetime)this).Invalidate();
        var controller = CurrentController;
        if (controller is not null) {
            await controller.CloseAsync(CancellationToken.None);
        }
        await WaitForCleanup(CancellationToken.None);
        DisposeLifetime();
    }

    private static async Task CompleteOperation(Func<Task> operation, TaskCompletionSource completion) {
        try {
            await operation();
            completion.SetResult();
        }
        catch (Exception ex) {
            completion.SetException(ex);
            /*
             * All callers may have canceled their waits while native cleanup
             * continued. Observe the fault even when no caller remains.
             */
            _ = completion.Task.Exception;
        }
    }

    private Task CloseLifetime(CancellationToken token) {
        TaskCompletionSource completion = null;
        Task close;
        lock (ControllerLocker) {
            var needsClose = CloseTask is null || CloseTask.IsFaulted;
            if (needsClose) {
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                CloseTask = completion.Task;
            }
            close = CloseTask;
        }
        if (completion is not null) {
            _ = CompleteOperation(CloseCore, completion);
        }
        return close.WaitAsync(token);
    }

    private async Task DrainCleanup() {
        while (true) {
            PendingControllerCleanup cleanup;
            lock (ControllerLocker) {
                cleanup = PendingCleanups.Count == 0 ? null : PendingCleanups[0];
                if (cleanup is null) {
                    CleanupTask = null;
                }
            }
            if (cleanup is null) {
                return;
            }
            try {
                await cleanup.Task;
            }
            catch (Exception) {
                await cleanup.Controller.CloseAsync(CancellationToken.None);
            }
            lock (ControllerLocker) {
                PendingCleanups.Remove(cleanup);
            }
        }
    }

    internal const string Key = "FileSystemTerminal";

    internal string ProviderDirectorySnapshot { get; }
    internal IPanelCollection PanelCollection { get; }
    internal IPanel Panel { get; }

    internal IProvider ScopeProvider =>
        (IProvider)Provider;

    internal bool IsInvalidated {
        get {
            lock (ControllerLocker) {
                return Invalidated;
            }
        }
    }

    internal bool IsPanelOwnerPresent {
        get {
            var panelIsPresent = false;
            foreach (var panel in PanelCollection.AsEnumerable()) {
                if (ReferenceEquals(panel, Panel)) {
                    panelIsPresent = true;
                    break;
                }
            }
            var panelHasProvider = Panel.HasProvider(out FileSystemProvider activeProvider);
            var providerIsCurrent = panelHasProvider && ReferenceEquals(activeProvider, Provider);
            var panelOwnerIsCurrent = panelIsPresent && providerIsCurrent;
            return panelOwnerIsCurrent;
        }
    }

    internal bool IsPanelCurrent {
        get {
            var panelIsActive = ReferenceEquals(PanelCollection.Active, Panel);
            var panelOwnerIsPresent = IsPanelOwnerPresent;
            var panelIsCurrent = panelIsActive && panelOwnerIsPresent;
            return panelIsCurrent;
        }
    }

    internal bool IsLiveOwner {
        get {
            var isInvalidated = IsInvalidated;
            var panelOwnerIsPresent = IsPanelOwnerPresent;
            var liveOwnerIsCurrent = isInvalidated == false && panelOwnerIsPresent;
            return liveOwnerIsCurrent;
        }
    }

    internal CancellationToken LifetimeToken =>
        LifetimeTokenValue;

    internal long RequestGeneration {
        get {
            lock (ControllerLocker) {
                return RequestGenerationValue;
            }
        }
    }

    internal long BeginRequest() {
        lock (ControllerLocker) {
            if (Invalidated) {
                return -1;
            }
            return ++RequestGenerationValue;
        }
    }

    internal long AdvanceReleaseIntent() {
        lock (ControllerLocker) {
            return ++RequestGenerationValue;
        }
    }

    internal bool IsCurrentReleaseIntent(long generation) {
        lock (ControllerLocker) {
            var releaseIntentIsCurrent = generation == RequestGenerationValue;
            return releaseIntentIsCurrent;
        }
    }

    internal bool IsCurrentRequest(long generation) {
        lock (ControllerLocker) {
            var requestIsCurrent = Invalidated == false && generation == RequestGenerationValue;
            return requestIsCurrent;
        }
    }

    internal IFileSystemTerminalController CurrentController {
        get {
            lock (ControllerLocker) {
                return ControllerValue;
            }
        }
    }

    internal FileSystemTerminal(FileSystemProvider provider,
                                IPanelCollection panelCollection,
                                IPanel panel)
    : base(provider ?? throw new ArgumentNullException(nameof(provider))) {
        PanelCollection = panelCollection ?? throw new ArgumentNullException(nameof(panelCollection));
        Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        ProviderDirectorySnapshot = provider.Directory.FullName;
        Lifetime = new CancellationTokenSource();
        LifetimeTokenValue = Lifetime.Token;
    }

    internal async Task<IFileSystemTerminalController> WaitForController(TimeSpan timeout,
                                                                          CancellationToken token) {
        var delay = Task.Delay(timeout, token);
        while (token.IsCancellationRequested == false) {
            Task<IFileSystemTerminalController> changed;
            lock (ControllerLocker) {
                if (ControllerValue is not null) {
                    return ControllerValue;
                }
                changed = ControllerChanged.Task;
            }
            var completed = await Task.WhenAny(changed, delay);
            if (completed != changed) {
                return null;
            }
            await changed;
        }
        return null;
    }

    internal void RecordControllerCleanup(IFileSystemTerminalController controller, Task task) {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(task);
        lock (ControllerLocker) {
            PendingCleanups.Add(new PendingControllerCleanup(controller, task));
        }
    }

    internal Task WaitForCleanup(CancellationToken token) {
        TaskCompletionSource completion = null;
        Task cleanup;
        lock (ControllerLocker) {
            if (PendingCleanups.Count == 0) {
                return Task.CompletedTask;
            }
            var needsCleanup = CleanupTask is null || CleanupTask.IsCompleted;
            if (needsCleanup) {
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                CleanupTask = completion.Task;
            }
            cleanup = CleanupTask;
        }
        if (completion is not null) {
            _ = CompleteOperation(DrainCleanup, completion);
        }
        return cleanup.WaitAsync(token);
    }

    internal bool DetachController(IFileSystemTerminalController controller) {
        TaskCompletionSource<IFileSystemTerminalController> changed;
        lock (ControllerLocker) {
            if (ReferenceEquals(ControllerValue, controller) == false) {
                return false;
            }
            ControllerValue = null;
            changed = ControllerChanged;
            ControllerChanged = NewControllerChanged();
        }
        changed.TrySetResult(null);
        return true;
    }

    protected override ProviderDetail Clone(Provider provider) {
        return null;
    }

    IFileSystemTerminalController IControlled<IFileSystemTerminalController>.Controller {
        set {
            if (value is not null) {
                SetController(value);
            }
        }
    }

    void IProviderDetailLifetime.Invalidate() {
        CancellationTokenSource lifetime = null;
        lock (ControllerLocker) {
            if (Invalidated) {
                return;
            }
            Invalidated = true;
            RequestGenerationValue++;
            if (LifetimeDisposed == false) {
                lifetime = Lifetime;
            }
        }
        if (lifetime is null) {
            return;
        }
        try {
            lifetime.Cancel();
        }
        catch (ObjectDisposedException) {
        }
    }

    Task IProviderDetailLifetime.CloseAsync(CancellationToken token) {
        return CloseLifetime(token);
    }

    bool IProviderDetailFocus.HasKeyboardFocus =>
        CurrentController?.HasKeyboardFocus == true;

    bool IProviderDetailFocus.RequestFocus() {
        return CurrentController?.RequestFocus() == true;
    }

    IProvider IControllingCommandGesture.Provider =>
        ScopeProvider;

    bool IControllingCommandGesture.Allows(ICommand command) {
        return command is Terminal;
    }
}
