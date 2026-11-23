using Brows.Entries;
using Brows.Gui;
using Brows.Panels;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Providers;

/// <summary>
/// Owns the single lower view displayed for a provider.
/// </summary>
public sealed class ProviderDetailSet : Notifier, IControlled<IProviderDetailSetController> {
    private readonly Lock ShutdownLocker = new();
    /*
     * Admission also works without a WPF host. Keep these state commits atomic;
     * Transition serializes cleanup separately, without holding a lock across await.
     */
    private readonly Lock StateLocker = new();
    private readonly Provider Owner;
    private readonly SemaphoreSlim ToggleGate = new(1, 1);
    private readonly SemaphoreSlim Transition = new(1, 1);
    /*
     * Shutdown joins admitted work after releasing Transition, including
     * toggle-owned candidate cleanup.
     */
    private readonly HashSet<Task> AdmittedWork = new();
    private readonly Dictionary<ProviderDetail, Exception> FailedCandidates = new(ReferenceEqualityComparer.Instance);
    /*
     * Remember retirement without keeping discarded details alive. Failed cleanup
     * stays strongly held for retry.
     */
    private readonly ConditionalWeakTable<ProviderDetail, object> RetiredCandidates = new();
    private readonly HashSet<ProviderDetail> RetiringCandidates = new(ReferenceEqualityComparer.Instance);

    private ActiveView ActiveValue;
    private IProviderDetailSetController ControllerValue;
    private Exception ErrorValue;
    private long GenerationValue;
    private PendingView PendingValue;
    private int ShutdownAdmission;
    private Task ShutdownTask;
    private int TransitioningValue;

    private sealed record ActiveView(object Key, ProviderDetail Detail);

    private sealed record PendingView(long Generation, object Key, ProviderDetail Detail);

    private async Task<T> OnController<T>(Func<Task<T>> work) {
        var controller = Controller;
        if (controller is null) {
            return await work();
        }

        var result = default(T);
        await controller.InvokeAsync(async () => {
            result = await work();
        });
        return result;
    }

    private bool TrySetActive(ActiveView value, long generation) {
        ActiveView previous;
        lock (StateLocker) {
            var generationIsCurrent = generation == GenerationValue;
            var shutdownIsNotRequested = ShutdownAdmission == 0;
            var publicationIsAllowed = generationIsCurrent && shutdownIsNotRequested;
            if (publicationIsAllowed == false) {
                return false;
            }
            previous = ActiveValue;
            ActiveValue = value;
        }
        if (ReferenceEquals(previous, value) == false) {
            NotifyPropertyChanged(nameof(Active));
            NotifyPropertyChanged(nameof(ActiveKey));
        }
        return true;
    }

    private bool ClearActive(ActiveView expected) {
        ActiveView previous;
        lock (StateLocker) {
            if (ReferenceEquals(ActiveValue, expected) == false) {
                return false;
            }
            previous = ActiveValue;
            ActiveValue = null;
        }
        if (previous is not null) {
            NotifyPropertyChanged(nameof(Active));
            NotifyPropertyChanged(nameof(ActiveKey));
        }
        return true;
    }

    private void SetError(Exception value) {
        var errorChanged = false;
        lock (StateLocker) {
            var previous = ErrorValue;
            if (ReferenceEquals(previous, value) == false) {
                Volatile.Write(ref ErrorValue, value);
                errorChanged = true;
            }
        }
        if (errorChanged) {
            NotifyPropertyChanged(nameof(Error));
        }
    }

    private void SetErrorIfCurrent(long generation, Exception value) {
        var errorChanged = false;
        lock (StateLocker) {
            if (generation != GenerationValue) {
                return;
            }
            var previous = ErrorValue;
            if (ReferenceEquals(previous, value) == false) {
                Volatile.Write(ref ErrorValue, value);
                errorChanged = true;
            }
        }
        if (errorChanged) {
            NotifyPropertyChanged(nameof(Error));
        }
    }

    private long BeginTransition(
        object key, ProviderDetail detail, Task admittedShow = null, bool allowShutdown = false) {
        long generation;
        lock (StateLocker) {
            var shutdownIsBlocking = ShutdownAdmission != 0 && allowShutdown == false;
            if (shutdownIsBlocking) {
                return 0;
            }
            var candidateWasRetired = detail is not null && RetiredCandidates.TryGetValue(detail, out _);
            if (candidateWasRetired) {
                return 0;
            }
            generation = ++GenerationValue;
            if (admittedShow is not null) {
                AdmittedWork.Add(admittedShow);
            }
            PendingValue = detail is null
                ? null
                : new PendingView(generation, key, detail);
            TransitioningValue = 1;
        }
        return generation;
    }

    private long BeginCloseTransition(
        ProviderDetail expected,
        out bool matchesActive,
        out bool matchesPending) {
        long generation;
        lock (StateLocker) {
            var shutdownIsBlocking = ShutdownAdmission != 0;
            if (shutdownIsBlocking) {
                matchesActive = false;
                matchesPending = false;
                return 0;
            }

            var active = ActiveValue;
            var pending = PendingValue;
            matchesActive = ReferenceEquals(active?.Detail, expected);
            matchesPending = ReferenceEquals(pending?.Detail, expected);
            var expectedIsKnown = matchesActive || matchesPending;
            if (expectedIsKnown == false) {
                return 0;
            }

            generation = ++GenerationValue;
            PendingValue = null;
            TransitioningValue = 1;
        }
        NotifyPropertyChanged(nameof(Generation));
        SetError(null);
        return generation;
    }

    private void CompleteTransition(long generation) {
        lock (StateLocker) {
            if (generation != GenerationValue) {
                return;
            }
            PendingValue = null;
            TransitioningValue = 0;
        }
    }

    private bool IsLatest(long generation) {
        lock (StateLocker) {
            return generation == GenerationValue && ShutdownAdmission == 0;
        }
    }

    private bool ProviderIsCurrent() {
        var panel = Owner.Panel;
        if (panel is null) {
            return false;
        }
        var panelHasProvider = panel.HasProvider(out var provider);
        if (panelHasProvider == false) {
            return false;
        }
        return ReferenceEquals(provider, Owner);
    }

    private static bool HasKeyboardFocus(ProviderDetail detail) {
        try {
            return detail is IProviderDetailFocus focus && focus.HasKeyboardFocus;
        }
        catch (Exception) {
            return false;
        }
    }

    private async Task<Exception> CloseActive(ActiveView active, long generation, bool returnFocus) {
        var focusWasOwned = HasKeyboardFocus(active.Detail);
        Exception error = null;
        if (active.Detail is IProviderDetailLifetime lifetime) {
            try {
                lifetime.Invalidate();
            }
            catch (Exception ex) {
                error = ex;
            }
            try {
                await lifetime.CloseAsync(CancellationToken.None);
            }
            catch (Exception ex) {
                error ??= ex;
            }
        }

        if (error is not null) {
            SetError(error);
            return error;
        }

        var focusStillOwned = HasKeyboardFocus(active.Detail);
        ClearActive(active);

        var shouldReturnFocus = returnFocus &&
                                focusWasOwned &&
                                focusStillOwned &&
                                generation == Generation &&
                                ProviderIsCurrent();
        if (shouldReturnFocus) {
            ((IProvider)Owner).Focus();
        }
        return null;
    }

    private async Task<Exception> RetireCandidate(ProviderDetail detail) {
        lock (StateLocker) {
            var isStillNeeded = ReferenceEquals(ActiveValue?.Detail, detail) ||
                                ReferenceEquals(PendingValue?.Detail, detail);
            var wasRetired = RetiredCandidates.TryGetValue(detail, out _);
            var needsRetry = FailedCandidates.ContainsKey(detail);
            var retirementIsAlreadyComplete = wasRetired && needsRetry == false;
            var retirementIsUnnecessary = isStillNeeded || retirementIsAlreadyComplete;
            if (retirementIsUnnecessary) {
                return null;
            }
            if (RetiringCandidates.Add(detail) == false) {
                return null;
            }
            if (wasRetired == false) {
                RetiredCandidates.Add(detail, new object());
            }
        }
        if (detail is not IProviderDetailLifetime lifetime) {
            lock (StateLocker) {
                RetiringCandidates.Remove(detail);
            }
            return null;
        }

        Exception error = null;
        try {
            lifetime.Invalidate();
        }
        catch (Exception ex) {
            error = ex;
        }
        try {
            await lifetime.CloseAsync(CancellationToken.None);
        }
        catch (Exception ex) {
            error ??= ex;
        }
        lock (StateLocker) {
            RetiringCandidates.Remove(detail);
            if (error is null) {
                FailedCandidates.Remove(detail);
            }
            else {
                FailedCandidates[detail] = error;
            }
        }
        return error;
    }

    private async Task<bool> Show(object key, ProviderDetail detail, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        ActiveView active;
        bool sameDetail;
        bool sameKey;
        bool transitionIsInProgress;
        lock (StateLocker) {
            if (ShutdownAdmission != 0) {
                return false;
            }
            active = ActiveValue;
            sameDetail = ReferenceEquals(active?.Detail, detail);
            sameKey = active is not null && Equals(active.Key, key);
            transitionIsInProgress = TransitioningValue != 0;
        }
        var sameDetailAndKey = sameDetail && sameKey;
        if (sameDetailAndKey) {
            if (transitionIsInProgress) {
                return false;
            }
            return true;
        }
        if (sameDetail) {
            throw new ArgumentException("A provider detail cannot be shown under multiple keys.", nameof(key));
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var generation = 0L;
        var entered = false;
        try {
            generation = BeginTransition(key, detail, completion.Task);
            if (generation == 0) {
                return false;
            }
            NotifyPropertyChanged(nameof(Generation));
            SetError(null);
            await Transition.WaitAsync(token);
            entered = true;

            if (IsLatest(generation) == false) {
                var staleCandidateError = await RetireCandidate(detail);
                if (staleCandidateError is not null) {
                    SetErrorIfCurrent(generation, staleCandidateError);
                }
                return false;
            }

            token.ThrowIfCancellationRequested();

            active = Volatile.Read(ref ActiveValue);
            var sameDetailBecameActive = ReferenceEquals(active?.Detail, detail);
            var sameKeyBecameActive = active is not null && Equals(active.Key, key);
            var sameDetailAndKeyBecameActive = sameDetailBecameActive && sameKeyBecameActive;
            if (sameDetailAndKeyBecameActive) {
                CompleteTransition(generation);
                return true;
            }
            if (active is not null) {
                var closeError = await CloseActive(active, generation, returnFocus: true);
                if (closeError is not null) {
                    CompleteTransition(generation);
                    return false;
                }
            }

            token.ThrowIfCancellationRequested();
            var requestIsStillCurrent = IsLatest(generation);
            if (requestIsStillCurrent == false) {
                var supersededCandidateError = await RetireCandidate(detail);
                if (supersededCandidateError is not null) {
                    SetErrorIfCurrent(generation, supersededCandidateError);
                }
                return false;
            }

            var activeWasPublished = TrySetActive(new ActiveView(key, detail), generation);
            if (activeWasPublished == false) {
                await RetireCandidate(detail);
                return false;
            }
            CompleteTransition(generation);
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            CompleteTransition(generation);
            var cancellationCandidateError = await RetireCandidate(detail);
            if (cancellationCandidateError is not null) {
                SetErrorIfCurrent(generation, cancellationCandidateError);
            }
            throw;
        }
        catch (Exception ex) {
            CompleteTransition(generation);
            var candidateError = await RetireCandidate(detail);
            SetErrorIfCurrent(generation, candidateError ?? ex);
            return false;
        }
        finally {
            if (entered) {
                Transition.Release();
            }
            lock (StateLocker) {
                AdmittedWork.Remove(completion.Task);
            }
            completion.TrySetResult();
        }
    }

    private async Task<bool> Close(ProviderDetail expected, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        var generation = BeginCloseTransition(
            expected,
            out var matchesActive,
            out var matchesPending);
        if (generation == 0) {
            return false;
        }
        var entered = false;
        try {
            await Transition.WaitAsync(token);
            entered = true;

            if (generation != Generation) {
                return false;
            }

            var active = Volatile.Read(ref ActiveValue);
            if (ReferenceEquals(active?.Detail, expected)) {
                var closeError = await CloseActive(active, generation, returnFocus: true);
                CompleteTransition(generation);
                return closeError is null;
            }

            if (matchesPending) {
                var candidateError = await RetireCandidate(expected);
                if (candidateError is not null) {
                    SetError(candidateError);
                    CompleteTransition(generation);
                    return false;
                }
                CompleteTransition(generation);
                return true;
            }

            var matchedActiveWasRemoved = matchesActive && active is null;
            if (matchedActiveWasRemoved) {
                CompleteTransition(generation);
                return true;
            }

            CompleteTransition(generation);
            return false;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            if (generation == Generation) {
                CompleteTransition(generation);
            }
            throw;
        }
        catch (Exception ex) {
            if (generation == Generation) {
                SetError(ex);
                CompleteTransition(generation);
            }
            return false;
        }
        finally {
            if (entered) {
                Transition.Release();
            }
        }
    }

    private async Task<bool> Toggle(object key, Func<ProviderDetail> factory, CancellationToken token) {
        await ToggleGate.WaitAsync(token);
        TaskCompletionSource completion = null;
        try {
            ActiveView active;
            lock (StateLocker) {
                if (ShutdownAdmission != 0) {
                    return false;
                }
                active = ActiveValue;
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                AdmittedWork.Add(completion.Task);
            }

            var requestedDetailIsActive = active is not null && Equals(active.Key, key);
            if (requestedDetailIsActive) {
                return await Close(active.Detail, token);
            }

            var detail = factory();
            ValidateDetail(Owner, detail, nameof(factory));
            try {
                return await Show(key, detail, token);
            }
            finally {
                var retirementError = await RetireCandidate(detail);
                if (retirementError is not null) {
                    SetError(retirementError);
                }
            }
        }
        finally {
            if (completion is not null) {
                lock (StateLocker) {
                    AdmittedWork.Remove(completion.Task);
                }
                completion.TrySetResult();
            }
            ToggleGate.Release();
        }
    }

    private async Task ShutdownCore() {
        var generation = BeginTransition(null, null, allowShutdown: true);
        ProviderDetail[] retryCandidates;
        lock (StateLocker) {
            retryCandidates = [.. FailedCandidates.Keys];
        }
        var entered = false;
        try {
            NotifyPropertyChanged(nameof(Generation));
            SetError(null);
            await Transition.WaitAsync();
            entered = true;

            var active = Volatile.Read(ref ActiveValue);
            Exception activeCloseError = null;
            if (active is not null) {
                activeCloseError = await CloseActive(active, generation, returnFocus: false);
            }

            Task[] admittedWork;
            lock (StateLocker) {
                admittedWork = [.. AdmittedWork];
            }
            Transition.Release();
            entered = false;
            await Task.WhenAll(admittedWork);
            if (activeCloseError is not null) {
                throw activeCloseError;
            }

            KeyValuePair<ProviderDetail, Exception>[] failedCandidates;
            lock (StateLocker) {
                failedCandidates = [.. FailedCandidates];
            }
            foreach (var failedCandidate in failedCandidates) {
                var wasFailedBeforeShutdown = Array.Exists(
                    retryCandidates,
                    candidate => ReferenceEquals(candidate, failedCandidate.Key));
                if (wasFailedBeforeShutdown == false) {
                    throw failedCandidate.Value;
                }
                var retirementError = await RetireCandidate(failedCandidate.Key);
                if (retirementError is not null) {
                    throw retirementError;
                }
            }

            lock (StateLocker) {
                PendingValue = null;
                TransitioningValue = 0;
                ShutdownAdmission = 2;
            }
            SetError(null);
        }
        catch (Exception ex) {
            lock (StateLocker) {
                PendingValue = null;
                TransitioningValue = 0;
                ShutdownAdmission = 0;
            }
            SetError(ex);
            throw;
        }
        finally {
            if (entered) {
                Transition.Release();
            }
        }
    }

    private async Task CompleteShutdown(TaskCompletionSource completion) {
        try {
            await OnController(async () => {
                await ShutdownCore();
                return true;
            });
            completion.TrySetResult();
        }
        catch (Exception ex) {
            /*
             * Reset this attempt before publishing its fault. A caller may retry as soon
             * as the task faults, so the old attempt must not reset newer admission state.
             */
            lock (StateLocker) {
                if (ShutdownAdmission == 1) {
                    ShutdownAdmission = 0;
                }
            }
            SetError(ex);
            completion.TrySetException(ex);
            lock (ShutdownLocker) {
                if (ReferenceEquals(ShutdownTask, completion.Task)) {
                    ShutdownTask = null;
                }
            }
        }
    }
    private static void ValidateDetail(Provider owner, ProviderDetail detail, string parameterName) {
        ArgumentNullException.ThrowIfNull(detail);
        if (ReferenceEquals(detail.Provider, owner) == false) {
            throw new ArgumentException("The provider detail belongs to another provider.", parameterName);
        }
    }

    internal IProviderDetailSetController Controller =>
        Volatile.Read(ref ControllerValue);

    internal bool DetachController(IProviderDetailSetController expected) {
        if (expected is null) {
            return false;
        }
        var previous = Interlocked.CompareExchange(ref ControllerValue, null, expected);
        return ReferenceEquals(previous, expected);
    }

    internal void Inherit(Provider previous) {
        ArgumentNullException.ThrowIfNull(previous);
        var previousSet = previous.Detail;
        var previousActive = Volatile.Read(ref previousSet.ActiveValue);
        if (previousActive is null) {
            return;
        }

        var previousGeneration = previousSet.Generation;
        var previousIsCurrent = previousSet.IsCurrent(previousActive.Detail, previousGeneration);
        if (previousIsCurrent == false) {
            return;
        }

        var inherited = previousActive.Detail.For(Owner);
        if (inherited is null) {
            return;
        }
        if (ReferenceEquals(inherited.Provider, Owner) == false) {
            throw new InvalidOperationException("A provider detail clone must belong to its destination provider.");
        }

        previousIsCurrent = previousSet.IsCurrent(previousActive.Detail, previousGeneration);
        if (previousIsCurrent == false) {
            return;
        }
        var inheritedGeneration = 0L;
        lock (StateLocker) {
            var destinationIsAvailable = ShutdownAdmission == 0 && ActiveValue is null;
            if (destinationIsAvailable) {
                inheritedGeneration = ++GenerationValue;
                ActiveValue = new ActiveView(previousActive.Key, inherited);
            }
        }
        if (inheritedGeneration != 0) {
            NotifyPropertyChanged(nameof(Generation));
            NotifyPropertyChanged(nameof(Active));
            NotifyPropertyChanged(nameof(ActiveKey));
        }
    }

    /// <summary>
    /// Gets the currently displayed detail, or <see langword="null"/> when the lower view is closed.
    /// </summary>
    public ProviderDetail Active =>
        Volatile.Read(ref ActiveValue)?.Detail;

    /// <summary>
    /// Gets the stable key of the currently displayed detail.
    /// </summary>
    public object ActiveKey =>
        Volatile.Read(ref ActiveValue)?.Key;

    /// <summary>
    /// Gets the generation that invalidates deferred focus requests when a transition is admitted.
    /// </summary>
    public long Generation =>
        Interlocked.Read(ref GenerationValue);

    /// <summary>
    /// Gets the most recent transition or cleanup error.
    /// </summary>
    public Exception Error =>
        Volatile.Read(ref ErrorValue);

    /// <summary>
    /// Gets the active detail when its key matches the supplied key.
    /// </summary>
    /// <param name="key">
    /// The key identifying the detail to retrieve.
    /// </param>
    /// <returns>
    /// The active detail with this key, or null if it is not active.
    /// </returns>
    public ProviderDetail this[object key] {
        get {
            var active = Volatile.Read(ref ActiveValue);
            return active is not null && Equals(active.Key, key)
                ? active.Detail
                : null;
        }
    }

    /// <summary>
    /// Initializes a new provider detail set for its owning provider.
    /// </summary>
    /// <param name="owner">
    /// The provider that owns the detail set.
    /// </param>
    public ProviderDetailSet(Provider owner) {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    /// <summary>
    /// Shows a detail, closing the current detail before publishing its replacement.
    /// </summary>
    /// <param name="key">
    /// The key identifying this kind of detail.
    /// </param>
    /// <param name="detail">
    /// The detail to show. It must belong to the provider that owns this set.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the detail is shown; false if the request could not be applied.
    /// </returns>
    public Task<bool> ShowAsync(object key, ProviderDetail detail, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(key);
        ValidateDetail(Owner, detail, nameof(detail));
        return OnController(() => Show(key, detail, token));
    }

    /// <summary>
    /// Closes the expected detail without affecting a replacement.
    /// </summary>
    /// <param name="expected">
    /// The active or pending detail to close. It must belong to this set's provider.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the expected detail was closed or its pending display was withdrawn; otherwise, false.
    /// </returns>
    public Task<bool> CloseAsync(ProviderDetail expected, CancellationToken token) {
        ValidateDetail(Owner, expected, nameof(expected));
        return OnController(() => Close(expected, token));
    }

    /// <summary>
    /// Toggles a keyed detail. Overlapping toggles run in invocation order after the preceding transition completes.
    /// A canceled request waiting its turn does not change the view.
    /// </summary>
    /// <param name="key">
    /// The key of the detail to close if active, or show otherwise.
    /// </param>
    /// <param name="factory">
    /// Creates a detail owned by this set's provider when the key is not already active.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the toggle was applied; false if it could not be applied.
    /// </returns>
    public Task<bool> ToggleAsync(object key, Func<ProviderDetail> factory, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);
        return OnController(() => Toggle(key, factory, token));
    }

    /// <summary>
    /// Closes the active detail and retains completion for repeated shutdown requests.
    /// A failed close can be retried.
    /// </summary>
    /// <returns>
    /// A task that completes after active and pending detail resources have been closed.
    /// </returns>
    public Task ShutdownAsync() {
        TaskCompletionSource completion;
        lock (ShutdownLocker) {
            var shutdownIsInFlightOrComplete = ShutdownTask is not null &&
                                                ShutdownTask.IsFaulted == false &&
                                                ShutdownTask.IsCanceled == false;
            if (shutdownIsInFlightOrComplete) {
                return ShutdownTask;
            }

            completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            lock (StateLocker) {
                ShutdownAdmission = 1;
            }
            ShutdownTask = completion.Task;
        }
        _ = CompleteShutdown(completion);
        return completion.Task;
    }

    /// <summary>
    /// Checks whether a detail and generation still identify the active, stable view.
    /// </summary>
    /// <param name="detail">
    /// The detail whose view is being checked.
    /// </param>
    /// <param name="generation">
    /// The Generation value captured when the work was requested.
    /// </param>
    /// <returns>
    /// True if the detail is still active, the generation matches, and no view change or shutdown is pending.
    /// </returns>
    public bool IsCurrent(ProviderDetail detail, long generation) {
        if (detail is null) {
            return false;
        }
        lock (StateLocker) {
            var active = ActiveValue;
            if (active is null) {
                return false;
            }
            var generationIsCurrent = generation == GenerationValue;
            var transitionIsStable = TransitioningValue == 0;
            var shutdownIsNotRequested = ShutdownAdmission == 0;
            return ReferenceEquals(active.Detail, detail) &&
                   generationIsCurrent &&
                   transitionIsStable &&
                   shutdownIsNotRequested;
        }
    }

    IProviderDetailSetController IControlled<IProviderDetailSetController>.Controller {
        set {
            if (value is null) {
                var controller = Controller;
                if (controller is not null) {
                    DetachController(controller);
                }
                return;
            }
            Volatile.Write(ref ControllerValue, value);
        }
    }
}
