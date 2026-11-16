using Brows.Detail;
using Brows.Gui;
using Brows.Providers;
using Brows.Terminal;
using Brows.Windows;
using Brows.Windows.Controls;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PATH = System.IO.Path;

namespace Brows;

[TemplatePart(Name = TerminalPartName, Type = typeof(TerminalEmulator))]
[TemplatePart(Name = StatusPartName, Type = typeof(TextBlock))]
internal sealed class FileSystemTerminalControl : ModelView<FileSystemTerminal>, IFileSystemTerminalController {
    private const string TerminalPartName = "PART_Terminal";
    private const string StatusPartName = "PART_Status";

    private static readonly ILog Log = Logging.For(typeof(FileSystemTerminalControl));

    private readonly HashSet<TerminalEmulator> OwnedTerminals = [];
    private readonly Dictionary<TerminalEmulator, TerminalSessionLifecycle> Lifecycles = [];
    private readonly Func<TerminalEmulator, ITerminalSessionAdapter> SessionAdapterFactory;
    private readonly Dictionary<TerminalSessionLifecycle, long> LoggedFailureNotification = [];
    private TerminalSessionLifecycle SessionLifecycle;
    private TerminalEmulator Terminal;
    private TextBlock StatusText;
    private FileSystemTerminal Detail;
    /*
     * A template may rebind this host to a new model. Its next session must wait
     * for the previous model's session to finish stopping, including failed stops.
     */
    private Task PendingCleanup = Task.CompletedTask;
    private long AttachmentId;

    private event EventHandler ControllerLoaded;

    private bool IsCurrentViewAttachment(FileSystemTerminal detail,
                                         long attachmentId,
                                         long generation,
                                         CancellationToken token) {
        if (token.IsCancellationRequested) {
            return false;
        }
        var controlIsLoaded = IsLoaded;
        var terminalIsLoaded = Terminal?.IsLoaded == true;
        var controlIsVisible = IsVisible;
        var terminalIsVisible = Terminal?.IsVisible == true;
        var viewIsAvailable = controlIsLoaded && terminalIsLoaded && controlIsVisible && terminalIsVisible;
        if (viewIsAvailable == false) {
            return false;
        }
        var detailIsAttached = ReferenceEquals(Detail, detail);
        var attachmentIsCurrent = attachmentId == AttachmentId;
        var attachmentIsStale = detailIsAttached == false || attachmentIsCurrent == false;
        if (attachmentIsStale) {
            return false;
        }
        var dataContextIsCurrent = ReferenceEquals(ViewModel, detail);
        var controllerIsCurrent = ReferenceEquals(detail.CurrentController, this);
        var modelAttachmentIsCurrent = dataContextIsCurrent && controllerIsCurrent;
        if (modelAttachmentIsCurrent == false) {
            return false;
        }
        var managerIsCurrent = detail.Provider.Detail.IsCurrent(detail, generation);
        var panelIsCurrent = detail.IsPanelCurrent;
        var focusOwnerIsCurrent = managerIsCurrent && panelIsCurrent;
        return focusOwnerIsCurrent;
    }

    private bool IsCurrentAttachment(FileSystemTerminal detail,
                                     long attachmentId,
                                     long generation,
                                     long requestGeneration,
                                     CancellationToken token) {
        var viewAttachmentIsCurrent = IsCurrentViewAttachment(detail, attachmentId, generation, token);
        var requestIntentIsCurrent = detail.IsCurrentRequest(requestGeneration);
        var focusAttachmentIsCurrent = viewAttachmentIsCurrent && requestIntentIsCurrent;
        return focusAttachmentIsCurrent;
    }

    private bool IsCurrentReleaseAttachment(FileSystemTerminal detail,
                                            long attachmentId,
                                            long generation,
                                            long releaseIntentGeneration,
                                            CancellationToken token) {
        var viewAttachmentIsCurrent = IsCurrentViewAttachment(detail, attachmentId, generation, token);
        var releaseIntentIsCurrent = detail.IsCurrentReleaseIntent(releaseIntentGeneration);
        var releaseAttachmentIsCurrent = viewAttachmentIsCurrent && releaseIntentIsCurrent;
        return releaseAttachmentIsCurrent;
    }

    private bool IsCurrentSessionOwner(FileSystemTerminal detail,
                                       long attachmentId,
                                       CancellationToken token) {
        if (token.IsCancellationRequested) {
            return false;
        }
        var controlIsLoaded = IsLoaded;
        var terminalIsLoaded = Terminal?.IsLoaded == true;
        var viewIsLoaded = controlIsLoaded && terminalIsLoaded;
        if (viewIsLoaded == false) {
            return false;
        }
        var detailIsAttached = ReferenceEquals(Detail, detail);
        var attachmentIsCurrent = attachmentId == AttachmentId;
        var dataContextIsCurrent = ReferenceEquals(ViewModel, detail);
        var controllerIsCurrent = ReferenceEquals(detail.CurrentController, this);
        var ownershipIsCurrent = detailIsAttached &&
                                 attachmentIsCurrent &&
                                 dataContextIsCurrent &&
                                 controllerIsCurrent;
        if (ownershipIsCurrent == false) {
            return false;
        }
        var detailIsLive = detail.IsLiveOwner;
        var currentGeneration = detail.Provider.Detail.Generation;
        var managerStillOwnsDetail = detail.Provider.Detail.IsCurrent(detail, currentGeneration);
        var sessionOwnershipIsCurrent = detailIsLive && managerStillOwnsDetail;
        return sessionOwnershipIsCurrent;
    }

    private void Attach(FileSystemTerminal detail) {
        if (ReferenceEquals(Detail, detail)) {
            return;
        }
        Detach(Detail);
        Detail = detail;
        AttachmentId++;
        ((IControlled<IFileSystemTerminalController>)detail).Controller = this;
    }

    private void Detach(FileSystemTerminal detail) {
        var detailIsCurrent = ReferenceEquals(Detail, detail);
        var detachIsInvalid = detail is null || detailIsCurrent == false;
        if (detachIsInvalid) {
            return;
        }
        Detail = null;
        AttachmentId++;
        detail.DetachController(this);
    }

    private void StartCleanup(FileSystemTerminal detail) {
        var cleanup = CloseAsync(CancellationToken.None);
        detail.RecordControllerCleanup(this, cleanup);
        Volatile.Write(ref PendingCleanup, cleanup);
        _ = ObserveCleanup(cleanup);
    }

    private async Task WaitForPendingCleanup(CancellationToken token) {
        var cleanup = Volatile.Read(ref PendingCleanup);
        try {
            await cleanup.WaitAsync(token);
        }
        catch (Exception) when (token.IsCancellationRequested == false) {
            await CloseAsync(token);
            Volatile.Write(ref PendingCleanup, Task.CompletedTask);
        }
    }

    private async Task<bool> WaitForLayout(TerminalEmulator terminal, CancellationToken token) {
        var terminalHasLayout = terminal.ActualWidth > 0 && terminal.ActualHeight > 0;
        if (terminalHasLayout) {
            return true;
        }
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SizeChangedEventHandler sizeChanged = null;
        RoutedEventHandler unloaded = null;
        sizeChanged = (sender, args) => {
            var terminalHasLayout = terminal.ActualWidth > 0 && terminal.ActualHeight > 0;
            if (terminalHasLayout) {
                ready.TrySetResult(true);
            }
        };
        unloaded = (sender, args) => ready.TrySetResult(false);
        terminal.SizeChanged += sizeChanged;
        terminal.Unloaded += unloaded;
        using var registration = token.Register(() => ready.TrySetCanceled(token));
        try {
            var terminalHasLayoutAfterAttach = terminal.ActualWidth > 0 && terminal.ActualHeight > 0;
            if (terminalHasLayoutAfterAttach) {
                return true;
            }
            var timeout = Task.Delay(TimeSpan.FromSeconds(2), token);
            var completed = await Task.WhenAny(ready.Task, timeout);
            var layoutWasReported = completed == ready.Task;
            if (layoutWasReported == false) {
                return false;
            }
            return await ready.Task;
        }
        finally {
            terminal.SizeChanged -= sizeChanged;
            terminal.Unloaded -= unloaded;
        }
    }

    private static string PowerShellCommandLine() {
        var executable = PATH.Combine(
            Environment.SystemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        return $"\"{executable}\" -NoLogo -NoProfile -NoExit";
    }

    private Task<T> OnDispatcher<T>(Func<Task<T>> work, CancellationToken token) {
        if (Dispatcher.CheckAccess()) {
            return work();
        }
        return Dispatcher
            .InvokeAsync(work, DispatcherPriority.Normal, token)
            .Task
            .Unwrap();
    }

    private Task OnDispatcher(Func<Task> work) {
        if (Dispatcher.CheckAccess()) {
            return work();
        }
        return Dispatcher
            .InvokeAsync(work, DispatcherPriority.Normal)
            .Task
            .Unwrap();
    }

    private void SetStatus(string value) {
        if (StatusText is not null) {
            StatusText.Text = value;
        }
    }

    private async Task ObserveCleanup(Task cleanup) {
        try {
            await cleanup;
        }
        catch (Exception) {
            /*
             * The terminal lifecycle publishes and logs cleanup failures once.
             */
        }
    }

    private async Task<bool> OpenCoreAsync(CancellationToken token) {
        /*
         * Keep the exact template attachment across awaits. Retemplating must not
         * let a pending open start a replacement before the old part is stopped.
         */
        var terminal = Terminal;
        var lifecycle = SessionLifecycle;
        var attachmentId = AttachmentId;
        var detail = Detail;
        var openContextIsMissing = detail is null || terminal is null || lifecycle is null;
        if (openContextIsMissing) {
            return false;
        }
        await detail.WaitForCleanup(token);
        await WaitForPendingCleanup(token);
        return await OnDispatcher(async () => {
            var attachedDetail = Detail;
            var detailIsAttached = ReferenceEquals(attachedDetail, detail);
            if (detailIsAttached == false) {
                return false;
            }
            var sessionIsCurrent = IsCurrentSessionOwner(detail, attachmentId, token);
            var currentLifecycleIsAttached = ReferenceEquals(SessionLifecycle, lifecycle);
            var openOwnerIsCurrent = sessionIsCurrent && currentLifecycleIsAttached;
            if (openOwnerIsCurrent == false) {
                return false;
            }
            var currentState = lifecycle.State;
            var sessionIsLive = terminal.IsRunning && currentState == TerminalSessionState.Running;
            if (sessionIsLive) {
                SetStatus(FileSystemText.Value("FileSystemTerminal_Status_Running"));
                return true;
            }
            var layoutReady = await WaitForLayout(terminal, token);
            var ownershipRemainsCurrent = IsCurrentSessionOwner(detail, attachmentId, token);
            var viewCanStart = layoutReady && ownershipRemainsCurrent &&
                               ReferenceEquals(SessionLifecycle, lifecycle);
            if (viewCanStart == false) {
                return false;
            }
            terminal.AutoStart = false;
            terminal.WorkingDirectory = detail.ProviderDirectorySnapshot;
            terminal.CommandLine = PowerShellCommandLine();
            SetStatus(FileSystemText.Value("FileSystemTerminal_Status_Starting"));
            try {
                await lifecycle.OpenAsync(token);
            }
            catch (Exception) {
                return false;
            }
            var sessionRemainsOwned = IsCurrentSessionOwner(detail, attachmentId, CancellationToken.None) &&
                                     ReferenceEquals(SessionLifecycle, lifecycle);
            if (sessionRemainsOwned == false) {
                await lifecycle.CloseAsync(CancellationToken.None);
                return false;
            }
            var openCanReturnSuccess = token.IsCancellationRequested == false &&
                                     lifecycle.State == TerminalSessionState.Running;
            return openCanReturnSuccess;
        }, token);
    }

    private void This_Loaded(object sender, RoutedEventArgs e) {
        AttachViewModel();
        ControllerLoaded?.Invoke(this, EventArgs.Empty);
    }

    private void This_Unloaded(object sender, RoutedEventArgs e) {
        var detail = Detail;
        if (detail is not null) {
            StartCleanup(detail);
            Detach(detail);
        }
    }

    private void AttachViewModel() {
        var canAttach = IsLoaded && Terminal is not null && ViewModel is not null;
        if (canAttach) {
            Attach(ViewModel);
        }
    }

    private void Terminal_Exited(object sender, TerminalExitedEventArgs args) {
        var terminal = sender as TerminalEmulator;
        if (terminal is null) {
            return;
        }
        var lifecycleIsAvailable = Lifecycles.TryGetValue(terminal, out var lifecycle);
        if (lifecycleIsAvailable) {
            lifecycle.ProcessExited(args.ExitCode);
        }
    }

    private void Terminal_Failed(object sender, TerminalFailedEventArgs args) {
        var terminal = sender as TerminalEmulator;
        if (terminal is null) {
            return;
        }
        var lifecycleIsAvailable = Lifecycles.TryGetValue(terminal, out var lifecycle);
        if (lifecycleIsAvailable) {
            lifecycle.ProcessFailed(args.Exception, terminal.IsRunning);
        }
    }

    private void Lifecycle_Changed(object sender, TerminalSessionLifecycleEventArgs args) {
        if (Dispatcher.CheckAccess() == false) {
            Dispatcher.BeginInvoke(() => Lifecycle_Changed(sender, args));
            return;
        }
        var lifecycle = sender as TerminalSessionLifecycle;
        if (lifecycle is null) {
            return;
        }
        var failureIsReported = args.State == TerminalSessionState.Failed && args.Exception is not null;
        if (failureIsReported) {
            var failureWasLogged = LoggedFailureNotification.TryGetValue(lifecycle, out var failureId) &&
                                   failureId >= args.FailureId;
            if (failureWasLogged == false) {
                LoggedFailureNotification[lifecycle] = args.FailureId;
                if (Log.Error()) {
                    Log.Error(nameof(Lifecycle_Changed), args.Exception);
                }
            }
        }
        var lifecycleIsCurrent = ReferenceEquals(SessionLifecycle, lifecycle);
        var operationIsCurrent = args.OperationId == lifecycle.OperationId;
        var statusEventIsCurrent = lifecycleIsCurrent && operationIsCurrent;
        if (statusEventIsCurrent == false) {
            return;
        }
        if (args.State == TerminalSessionState.Starting) {
            SetStatus(FileSystemText.Value("FileSystemTerminal_Status_Starting"));
            return;
        }
        if (args.State == TerminalSessionState.Stopping) {
            SetStatus(FileSystemText.Value("FileSystemTerminal_Status_Stopping"));
            return;
        }
        if (args.State == TerminalSessionState.Running) {
            SetStatus(FileSystemText.Value("FileSystemTerminal_Status_Running"));
            return;
        }
        if (args.State == TerminalSessionState.Exited) {
            if (args.ExitCode.HasValue) {
                var format = FileSystemText.Value("FileSystemTerminal_Status_Exited");
                SetStatus(string.Format(format, args.ExitCode.Value));
            }
            else {
                SetStatus(FileSystemText.Value("FileSystemTerminal_Status_ExitedWithoutCode"));
            }
            return;
        }
        if (args.State == TerminalSessionState.Stopped) {
            SetStatus(FileSystemText.Value("FileSystemTerminal_Status_Stopped"));
            return;
        }
        if (failureIsReported) {
            var format = FileSystemText.Value("FileSystemTerminal_Status_Failed");
            SetStatus(string.Format(format, args.Exception.Message));
        }
    }

    internal TerminalEmulator TerminalView => Terminal;
    internal TextBlock Status => StatusText;

    internal FileSystemTerminalControl(Func<TerminalEmulator, ITerminalSessionAdapter> sessionAdapterFactory) {
        SessionAdapterFactory = sessionAdapterFactory ?? throw new ArgumentNullException(nameof(sessionAdapterFactory));
        Loaded += This_Loaded;
        Unloaded += This_Unloaded;
    }

    protected override void OnViewModelChanged(FileSystemTerminal oldValue, FileSystemTerminal newValue) {
        var detail = Detail;
        if (detail is not null) {
            StartCleanup(detail);
            Detach(detail);
        }
        base.OnViewModelChanged(oldValue, newValue);
        AttachViewModel();
    }

    public bool HasKeyboardFocus {
        get {
            if (Dispatcher.CheckAccess()) {
                return Terminal?.IsKeyboardFocusWithin == true;
            }
            return Dispatcher.Invoke(() => Terminal?.IsKeyboardFocusWithin == true);
        }
    }

    public FileSystemTerminalControl() : this(terminal => new TerminalEmulatorSessionAdapter(terminal)) {
    }

    public override void OnApplyTemplate() {
        var oldTerminal = Terminal;
        base.OnApplyTemplate();
        var newTerminal = GetTemplateChild(TerminalPartName) as TerminalEmulator;
        StatusText = GetTemplateChild(StatusPartName) as TextBlock;
        if (ReferenceEquals(oldTerminal, newTerminal)) {
            return;
        }
        var detail = Detail;
        Terminal = newTerminal;
        SessionLifecycle = null;
        AttachmentId++;
        if (detail is not null) {
            detail.AdvanceReleaseIntent();
            StartCleanup(detail);
            Detach(detail);
        }
        if (newTerminal is not null) {
            newTerminal.AutoStart = false;
            OwnedTerminals.Add(newTerminal);
            SessionLifecycle = new TerminalSessionLifecycle(SessionAdapterFactory(newTerminal));
            Lifecycles.Add(newTerminal, SessionLifecycle);
            SessionLifecycle.Changed += Lifecycle_Changed;
            newTerminal.TerminalExited += Terminal_Exited;
            newTerminal.TerminalFailed += Terminal_Failed;
        }
        AttachViewModel();
    }

    public bool RequestFocus() {
        if (Dispatcher.CheckAccess() == false) {
            return Dispatcher.Invoke(RequestFocus);
        }
        var detail = Detail;
        if (detail is null) {
            return false;
        }
        var generation = detail.Provider.Detail.Generation;
        var requestGeneration = detail.RequestGeneration;
        var focusRequestIsCurrent = IsCurrentAttachment(
            detail,
            AttachmentId,
            generation,
            requestGeneration,
            CancellationToken.None);
        if (focusRequestIsCurrent == false) {
            return false;
        }
        return Terminal.Focus();
    }

    public void RequestFocusLater(long generation, long requestGeneration, CancellationToken token) {
        var detail = Detail;
        var attachmentId = AttachmentId;
        if (detail is null) {
            return;
        }
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => {
            var attachmentIsCurrent = IsCurrentAttachment(
                detail, attachmentId, generation, requestGeneration, token);
            if (attachmentIsCurrent) {
                Terminal.Focus();
            }
        }));
    }

    public Task<bool> OpenAsync(CancellationToken token) {
        return OnDispatcher(() => OpenCoreAsync(token), token);
    }

    public async Task<bool> FocusEntriesAsync(long generation,
                                              long releaseIntentGeneration,
                                              CancellationToken token) {
        var detail = Detail;
        var attachmentId = AttachmentId;
        if (detail is null) {
            return false;
        }
        return await Dispatcher.InvokeAsync(() => {
            var attachmentIsCurrent = IsCurrentReleaseAttachment(
                detail, attachmentId, generation, releaseIntentGeneration, token);
            if (attachmentIsCurrent == false) {
                return false;
            }
            return ((IProvider)detail.Provider).Focus();
        }, DispatcherPriority.Input, token);
    }

    public async Task CloseAsync(CancellationToken token) {
        await OnDispatcher(async () => {
            /*
             * Retain failed old template parts so model-level cleanup retries
             * still stop their sessions after this control has new parts.
             */
            var terminals = new List<TerminalEmulator>(OwnedTerminals);
            List<Exception> errors = [];
            foreach (var terminal in terminals) {
                var terminalStopped = false;
                try {
                    if (Lifecycles.TryGetValue(terminal, out var lifecycle)) {
                        await lifecycle.CloseAsync(CancellationToken.None);
                    }
                    else {
                        await terminal.StopAsync();
                    }
                    terminalStopped = true;
                }
                catch (Exception error) {
                    errors.Add(error);
                }
                var terminalIsRetired = ReferenceEquals(terminal, Terminal) == false;
                var terminalCanBeReleased = terminalStopped && terminalIsRetired;
                if (terminalCanBeReleased) {
                    terminal.TerminalExited -= Terminal_Exited;
                    terminal.TerminalFailed -= Terminal_Failed;
                    if (Lifecycles.TryGetValue(terminal, out var retiredLifecycle)) {
                        retiredLifecycle.Changed -= Lifecycle_Changed;
                        LoggedFailureNotification.Remove(retiredLifecycle);
                    }
                    OwnedTerminals.Remove(terminal);
                    Lifecycles.Remove(terminal);
                }
            }
            if (errors.Count == 1) {
                throw errors[0];
            }
            if (errors.Count > 1) {
                throw new AggregateException(errors);
            }
            SetStatus(FileSystemText.Value("FileSystemTerminal_Status_Stopped"));
        });
    }
    event EventHandler IController.Loaded {
        add => ControllerLoaded += value;
        remove => ControllerLoaded -= value;
    }
}
