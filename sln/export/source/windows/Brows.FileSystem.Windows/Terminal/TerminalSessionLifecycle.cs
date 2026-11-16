using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Terminal;

internal sealed class TerminalSessionLifecycle {
    private readonly Lock Sync = new();
    private readonly ITerminalSessionAdapter Adapter;
    private TaskCompletionSource<bool> StartCompletion;
    private TaskCompletionSource<bool> StopCompletion;
    private TerminalSessionState CurrentState;
    private long CurrentOperationId;
    private int OpenWaiterCount;
    private bool StopRequested;
    private bool FailurePublished;
    private bool ExitReported;
    private uint? ExitCode;
    private Exception Failure;
    private Exception LastRuntimeFailure;
    private long NextFailureId;
    private bool OpenAccepted;

    private async Task WaitForOpenAsync(long operationId,
                                       TaskCompletionSource<bool> completion,
                                       Task<bool> startTask,
                                       CancellationToken token) {
        var callerCancelled = false;
        try {
            var startSucceeded = await startTask.WaitAsync(token);
            if (startSucceeded) {
                lock (Sync) {
                    if (operationId == CurrentOperationId) {
                        OpenAccepted = true;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            callerCancelled = true;
            throw;
        }
        finally {
            TerminalSessionLifecycleEventArgs change = null;
            TaskCompletionSource<bool> stopCompletion = null;
            lock (Sync) {
                if (operationId == CurrentOperationId && completion is not null && OpenWaiterCount > 0) {
                    OpenWaiterCount--;
                    var noOpenWaitersRemain = OpenWaiterCount == 0;
                    var startWasPendingOrCallerCancelled = startTask.IsCompleted == false || callerCancelled;
                    var everyOpenCallerWithdrew = noOpenWaitersRemain && startWasPendingOrCallerCancelled;
                    var automaticStopIsRequired = everyOpenCallerWithdrew &&
                                                 OpenAccepted == false && StopRequested == false;
                    if (automaticStopIsRequired) {
                        StopRequested = true;
                        CurrentState = TerminalSessionState.Stopping;
                        change = CreateEventArgs(operationId);
                        if (StopCompletion is null || StopCompletion.Task.IsCompleted) {
                            stopCompletion = new TaskCompletionSource<bool>(
                                TaskCreationOptions.RunContinuationsAsynchronously);
                            StopCompletion = stopCompletion;
                            _ = ObserveFault(stopCompletion.Task);
                        }
                    }
                }
            }
            Publish(change);
            if (stopCompletion is not null) {
                _ = StopCoreAsync(operationId, startTask, stopCompletion);
            }
        }
    }

    private async Task StartCoreAsync(long operationId, TaskCompletionSource<bool> completion) {
        await Task.Yield();
        try {
            await Adapter.StartAsync();
            TerminalSessionLifecycleEventArgs change = null;
            TaskCompletionSource<bool> automaticStopCompletion = null;
            var startSucceeded = false;
            lock (Sync) {
                if (operationId == CurrentOperationId) {
                    var stopWasRequested = StopRequested;
                    var startFailed = CurrentState == TerminalSessionState.Failed;
                    if (stopWasRequested) {
                        CurrentState = TerminalSessionState.Stopping;
                    }
                    else if (startFailed) {
                        var failedSessionIsRunning = Adapter.IsRunning;
                        if (failedSessionIsRunning) {
                            StopRequested = true;
                            CurrentState = TerminalSessionState.Stopping;
                            change = CreateEventArgs(operationId);
                            automaticStopCompletion = new TaskCompletionSource<bool>(
                                TaskCreationOptions.RunContinuationsAsynchronously);
                            StopCompletion = automaticStopCompletion;
                            _ = ObserveFault(automaticStopCompletion.Task);
                        }
                    }
                    else if (ExitReported) {
                        CurrentState = TerminalSessionState.Exited;
                    }
                    else {
                        var startProducedRunningSession = Adapter.IsRunning;
                        CurrentState = startProducedRunningSession
                            ? TerminalSessionState.Running
                            : TerminalSessionState.Exited;
                        startSucceeded = CurrentState == TerminalSessionState.Running;
                        change = CreateEventArgs(operationId);
                    }
                }
            }
            Publish(change);
            completion.TrySetResult(startSucceeded);
            lock (Sync) {
                if (ReferenceEquals(StartCompletion, completion)) {
                    StartCompletion = null;
                }
            }
            if (automaticStopCompletion is not null) {
                _ = StopCoreAsync(operationId, completion.Task, automaticStopCompletion);
            }
        }
        catch (Exception exception) {
            ReportFailure(operationId, exception);
            completion.TrySetException(exception);
            lock (Sync) {
                if (ReferenceEquals(StartCompletion, completion)) {
                    StartCompletion = null;
                }
            }
        }
    }

    private async Task StopCoreAsync(long operationId,
                                     Task<bool> startTask,
                                     TaskCompletionSource<bool> completion) {
        try {
            if (startTask is not null) {
                try {
                    await startTask;
                }
                catch (Exception) {
                    /*
                     * Stop still runs after a failed start so the adapter can release
                     * resources created before startup reported its failure.
                     */
                }
                TerminalSessionLifecycleEventArgs change = null;
                lock (Sync) {
                    if (operationId == CurrentOperationId) {
                        operationId = ++CurrentOperationId;
                        FailurePublished = false;
                        Failure = null;
                        CurrentState = TerminalSessionState.Stopping;
                        change = CreateEventArgs(operationId);
                    }
                }
                Publish(change);
            }
            await Adapter.StopAsync();
            TerminalSessionLifecycleEventArgs stopChange = null;
            lock (Sync) {
                if (operationId == CurrentOperationId) {
                    CurrentState = TerminalSessionState.Stopped;
                    stopChange = CreateEventArgs(operationId);
                }
                if (ReferenceEquals(StopCompletion, completion)) {
                    StopCompletion = null;
                }
            }
            Publish(stopChange);
            completion.TrySetResult(true);
        }
        catch (Exception exception) {
            ReportFailure(operationId, exception);
            lock (Sync) {
                if (ReferenceEquals(StopCompletion, completion)) {
                    StopCompletion = null;
                }
            }
            completion.TrySetException(exception);
        }
    }

    private void ReportFailure(long operationId, Exception exception) {
        TerminalSessionLifecycleEventArgs failureChange = null;
        lock (Sync) {
            var operationIsCurrent = operationId == CurrentOperationId;
            if (operationIsCurrent) {
                var failureIsNewForOperation = FailurePublished == false;
                FailurePublished = true;
                Failure = exception;
                CurrentState = TerminalSessionState.Failed;
                if (failureIsNewForOperation) {
                    var failureId = ++NextFailureId;
                    failureChange = CreateEventArgs(operationId, failureId: failureId);
                }
            }
        }
        Publish(failureChange);
    }

    private TerminalSessionLifecycleEventArgs CreateEventArgs(long operationId,
                                                              TerminalSessionState? state = null,
                                                              long failureId = 0) {
        var eventState = state ?? CurrentState;
        return new TerminalSessionLifecycleEventArgs(operationId, failureId, eventState, ExitCode, Failure);
    }

    private void Publish(TerminalSessionLifecycleEventArgs change) {
        if (change is not null) {
            Changed?.Invoke(this, change);
        }
    }

    private static async Task ObserveFault(Task task) {
        try {
            await task;
        }
        catch (Exception) {
            /* Lifecycle failures are reported through Changed and explicit callers. */
        }
    }

    internal event EventHandler<TerminalSessionLifecycleEventArgs> Changed;

    internal TerminalSessionState State {
        get {
            lock (Sync) {
                return CurrentState;
            }
        }
    }

    internal long OperationId {
        get {
            lock (Sync) {
                return CurrentOperationId;
            }
        }
    }

    internal TerminalSessionLifecycle(ITerminalSessionAdapter adapter) {
        Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        CurrentState = TerminalSessionState.NotStarted;
    }

    internal async Task OpenAsync(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        Task<bool> startTask = null;
        Task<bool> stopTask = null;
        TaskCompletionSource<bool> startCompletion = null;
        TerminalSessionLifecycleEventArgs change = null;
        long operationId = 0;
        var cleanupBeforeRestart = false;
        var startWasCreated = false;
        lock (Sync) {
            var terminalIsAlreadyRunning = CurrentState == TerminalSessionState.Running && Adapter.IsRunning;
            if (terminalIsAlreadyRunning) {
                OpenAccepted = true;
                return;
            }
            var cleanupIsNeeded = CurrentState is TerminalSessionState.Exited or TerminalSessionState.Failed;
            var startIsComplete = StartCompletion is null || StartCompletion.Task.IsCompleted;
            var stopIsComplete = StopCompletion is null || StopCompletion.Task.IsCompleted;
            var cleanupCanRunBeforeRestart = cleanupIsNeeded && startIsComplete && stopIsComplete;
            var stopIsPending = StopCompletion is not null && StopCompletion.Task.IsCompleted == false;
            var startIsPending = StartCompletion is not null && StartCompletion.Task.IsCompleted == false;
            if (cleanupCanRunBeforeRestart) {
                cleanupBeforeRestart = true;
            }
            else if (stopIsPending) {
                stopTask = StopCompletion.Task;
            }
            else if (startIsPending) {
                startCompletion = StartCompletion;
                startTask = startCompletion.Task;
                operationId = CurrentOperationId;
                OpenWaiterCount++;
            }
            else {
                startCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                StartCompletion = startCompletion;
                startTask = startCompletion.Task;
                OpenWaiterCount = 1;
                StopRequested = false;
                FailurePublished = false;
                ExitReported = false;
                ExitCode = null;
                Failure = null;
                LastRuntimeFailure = null;
                OpenAccepted = false;
                operationId = ++CurrentOperationId;
                CurrentState = TerminalSessionState.Starting;
                change = CreateEventArgs(operationId);
                startWasCreated = true;
            }
        }

        if (cleanupBeforeRestart) {
            await CloseAsync(token);
            await OpenAsync(token);
            return;
        }
        if (stopTask is not null) {
            await stopTask.WaitAsync(token);
            await OpenAsync(token);
            return;
        }
        Publish(change);
        if (startWasCreated) {
            _ = ObserveFault(startTask);
            _ = StartCoreAsync(operationId, startCompletion);
        }
        await WaitForOpenAsync(operationId, startCompletion, startTask, token);
    }

    internal async Task CloseAsync(CancellationToken token) {
        Task<bool> startTask;
        Task<bool> stopTask;
        TaskCompletionSource<bool> stopCompletion = null;
        TerminalSessionLifecycleEventArgs change = null;
        long operationId;
        lock (Sync) {
            if (CurrentState is TerminalSessionState.Stopped or TerminalSessionState.NotStarted) {
                CurrentState = TerminalSessionState.Stopped;
                return;
            }
            startTask = StartCompletion?.Task;
            operationId = CurrentOperationId;
            var stopIsAlreadyInProgress = StopCompletion is not null &&
                                          StopCompletion.Task.IsCompleted == false;
            var newStopAttemptIsRequired = startTask is null && stopIsAlreadyInProgress == false;
            if (newStopAttemptIsRequired) {
                operationId = ++CurrentOperationId;
                FailurePublished = false;
                Failure = null;
                LastRuntimeFailure = null;
                OpenAccepted = false;
                ExitCode = null;
                ExitReported = false;
            }
            StopRequested = true;
            OpenAccepted = false;
            if (CurrentState != TerminalSessionState.Stopping) {
                CurrentState = TerminalSessionState.Stopping;
                change = CreateEventArgs(operationId);
            }
            var stopIsPending = StopCompletion is not null && StopCompletion.Task.IsCompleted == false;
            if (stopIsPending) {
                stopTask = StopCompletion.Task;
            }
            else {
                stopCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                StopCompletion = stopCompletion;
                stopTask = stopCompletion.Task;
            }
        }

        Publish(change);
        if (stopCompletion is not null) {
            _ = ObserveFault(stopTask);
            _ = StopCoreAsync(operationId, startTask, stopCompletion);
        }
        await stopTask.WaitAsync(token);
    }

    /*
     * Each helper belongs to one package control. The host retains retired control
     * event handlers until cleanup completes. Package callbacks reject old sessions;
     * this helper also ignores events once stopping or stopped has begun.
     */
    internal void ProcessExited(uint? exitCode) {
        TerminalSessionLifecycleEventArgs change = null;
        lock (Sync) {
            var eventBelongsToActiveSession = CurrentState is TerminalSessionState.Starting or
                TerminalSessionState.Running;
            var firstExitForSession = eventBelongsToActiveSession && ExitReported == false;
            if (firstExitForSession) {
                ExitReported = true;
                ExitCode = exitCode;
                CurrentState = TerminalSessionState.Exited;
                change = CreateEventArgs(CurrentOperationId);
            }
        }
        Publish(change);
    }

    /*
     * A Failed notification can describe a nonfatal input/render error while the process
     * remains Running. The event reports that error, while State keeps ownership accurate.
     * A later fatal event or explicit call exception still transitions State to Failed.
     */
    internal void ProcessFailed(Exception exception) {
        ProcessFailed(exception, Adapter.IsRunning);
    }

    internal void ProcessFailed(Exception exception, bool sessionIsRunning) {
        if (exception is null) {
            throw new ArgumentNullException(nameof(exception));
        }
        TerminalSessionLifecycleEventArgs change = null;
        lock (Sync) {
            var eventBelongsToActiveSession = CurrentState is TerminalSessionState.Starting or
                TerminalSessionState.Running or TerminalSessionState.Exited;
            if (eventBelongsToActiveSession) {
                var failureRequiresStoppedState = sessionIsRunning == false ||
                                                  CurrentState == TerminalSessionState.Exited;
                var failureIsRuntimeScoped = CurrentState is TerminalSessionState.Running or
                    TerminalSessionState.Exited;
                var sameRuntimeFailure = ReferenceEquals(LastRuntimeFailure, exception);
                var failureIsNewForOperation = FailurePublished == false;
                var failureNeedsNotification = failureIsRuntimeScoped
                    ? sameRuntimeFailure == false || failureRequiresStoppedState
                    : failureIsNewForOperation;
                if (failureIsRuntimeScoped && failureNeedsNotification) {
                    LastRuntimeFailure = exception;
                }
                Failure = exception;
                if (failureRequiresStoppedState) {
                    CurrentState = TerminalSessionState.Failed;
                }
                if (failureNeedsNotification) {
                    FailurePublished = true;
                    var failureId = ++NextFailureId;
                    var notificationState = failureRequiresStoppedState
                        ? CurrentState
                        : TerminalSessionState.Failed;
                    change = CreateEventArgs(CurrentOperationId, notificationState, failureId);
                }
            }
        }
        Publish(change);
    }

}
