using Brows.Commands;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Entries;

internal sealed class EntryData : Notifier, IEntryData, ICommandSourceObject {
    private static readonly ILog Log = Logging.For(typeof(EntryData));
    private static readonly PropertyChangedEventArgs AccessingEvent = new(nameof(Accessing));
    private static readonly PropertyChangedEventArgs AccessingFlagEvent = new(nameof(AccessingFlag));
    private static readonly PropertyChangedEventArgs AccessCanceledEvent = new(nameof(AccessCanceled));
    private static readonly PropertyChangedEventArgs AccessErrorEvent = new(nameof(AccessError));
    private static readonly PropertyChangedEventArgs AccessExceptionEvent = new(nameof(AccessException));
    private static readonly PropertyChangedEventArgs DisplayEvent = new(nameof(Display));
    private static readonly PropertyChangedEventArgs ReadyEvent = new(nameof(Ready));
    private static readonly PropertyChangedEventArgs ValueEvent = new(nameof(Value));
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(100);

    private readonly Lock StateLock = new();
    private readonly Lock PublicationLock = new();
    private readonly TimeProvider Clock;
    private volatile object CurrentValue;
    private LoadState Load;
    private Task RefreshBarrier = Task.CompletedTask;

    private bool IsCurrent(LoadState state) {
        lock (StateLock) {
            return ReferenceEquals(Load, state);
        }
    }

    private void Status(LoadState state, Action change) {
        lock (PublicationLock) {
            if (IsCurrent(state)) {
                change();
            }
        }
    }

    private Task EnsureReady() {
        LoadState state;
        Task barrier;
        lock (StateLock) {
            if (Load is not null) {
                return Load.Completion.Task;
            }
            Load = state = new();
            barrier = RefreshBarrier;
        }
        _ = LoadValue(state, barrier);
        return state.Completion.Task;
    }

    private async Task FlagAccess(LoadState state) {
        try {
            await Task.Delay(UpdateInterval, Clock, Token).ConfigureAwait(false);
            Status(state, () => {
                if (Accessing) {
                    AccessingFlag = true;
                }
            });
        }
        catch (OperationCanceledException) when (Token.IsCancellationRequested) {
        }
    }

    private void StopProgress(LoadState state) {
        lock (StateLock) {
            state.Ended = true;
            state.Timer?.Dispose();
            state.Timer = null;
        }
    }

    private void Publish(LoadState state) {
        NotifyPropertyChanged(ValueEvent);
        if (IsCurrent(state)) {
            NotifyPropertyChanged(DisplayEvent);
        }
    }

    private void Progress(LoadState state, object value) {
        bool first;
        lock (StateLock) {
            var accepts = ReferenceEquals(Load, state) && !state.Ended && !Token.IsCancellationRequested;
            if (!accepts) {
                return;
            }
            if (Equals(CurrentValue, value)) {
                return;
            }
            CurrentValue = value;
            state.Dirty = true;
            first = !state.Published;
            if (!first) {
                var remaining = UpdateInterval - Clock.GetElapsedTime(state.LastPublication);
                state.Timer ??= Clock.CreateTimer(_ => Flush(state), null,
                    Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                if (!state.Scheduled) {
                    state.Scheduled = true;
                    state.Timer.Change(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
                        Timeout.InfiniteTimeSpan);
                }
            }
        }
        if (first) {
            Flush(state);
        }
    }

    private void Flush(LoadState state) {
        lock (PublicationLock) {
            lock (StateLock) {
                state.Scheduled = false;
                var publishes = ReferenceEquals(Load, state) && !state.Ended && state.Dirty;
                if (!publishes) {
                    return;
                }
                var remaining = UpdateInterval - Clock.GetElapsedTime(state.LastPublication);
                var tooEarly = state.Published && remaining > TimeSpan.Zero;
                if (tooEarly) {
                    state.Timer ??= Clock.CreateTimer(_ => Flush(state), null,
                        Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    state.Scheduled = true;
                    state.Timer.Change(remaining, Timeout.InfiniteTimeSpan);
                    return;
                }
                state.Dirty = false;
                state.Published = true;
                state.LastPublication = Clock.GetTimestamp();
            }
            Publish(state);
        }
    }

    private async Task LoadValue(LoadState state, Task barrier) {
        object result = null;
        Exception error = null;
        var canceled = false;
        using var registration = Token.Register(() => StopProgress(state));
        try {
            await barrier.ConfigureAwait(false);
            Token.ThrowIfCancellationRequested();
            if (!IsCurrent(state)) {
                return;
            }
            Status(state, () => Accessing = true);
            Status(state, () => AccessError = false);
            Status(state, () => AccessCanceled = false);
            Status(state, () => AccessException = null);
            _ = FlagAccess(state);
            result = await Definition.GetValue(Entry, value => Progress(state, value), Token).ConfigureAwait(false);
            Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (Token.IsCancellationRequested) {
            canceled = true;
            result = null;
        }
        catch (Exception ex) {
            error = ex;
            if (Log.Warn()) {
                Log.Warn(ex);
            }
        }
        finally {
            try {
                lock (PublicationLock) {
                    StopProgress(state);
                    var publish = false;
                    lock (StateLock) {
                        if (ReferenceEquals(Load, state)) {
                            publish = state.Dirty || !Equals(CurrentValue, result);
                            CurrentValue = result;
                            state.Dirty = false;
                        }
                    }
                    Status(state, () => AccessException = error);
                    Status(state, () => AccessError = error is not null);
                    Status(state, () => AccessCanceled = canceled);
                    Status(state, () => Accessing = false);
                    Status(state, () => AccessingFlag = false);
                    var publishes = publish && IsCurrent(state);
                    if (publishes) {
                        Publish(state);
                    }
                }
            }
            finally {
                state.Completion.TrySetResult();
            }
        }
    }

    private sealed class LoadState {
        internal readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ITimer Timer;
        internal bool Ended;
        internal bool Dirty;
        internal bool Scheduled;
        internal bool Published;
        internal long LastPublication;
    }

    private static async Task CompleteCanceledRefreshBarrier(Task previous, TaskCompletionSource completion) {
        try {
            await previous.ConfigureAwait(false);
            completion.TrySetResult();
        }
        catch (Exception ex) {
            completion.TrySetException(ex);
            _ = completion.Task.Exception;
        }
    }

    internal EntryData(IEntry entry, IEntryDataDefinition definition, TimeProvider clock, CancellationToken token) {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Token = token;
    }

    public bool Accessing {
        get;
        private set => Change(ref field, value, AccessingEvent);
    }

    public bool AccessingFlag {
        get;
        private set => Change(ref field, value, AccessingFlagEvent);
    }

    public bool AccessCanceled {
        get;
        private set => Change(ref field, value, AccessCanceledEvent);
    }

    public bool AccessError {
        get;
        private set => Change(ref field, value, AccessErrorEvent);
    }

    public Exception AccessException {
        get;
        private set => Change(ref field, value, AccessExceptionEvent);
    }

    public object Value => CurrentValue;

    public object Display {
        get {
            var converter = Definition.Converter;
            return converter is null
                ? Value
                : converter.Convert(Value, Definition.ConverterParameter, CultureInfo.CurrentUICulture);
        }
    }

    public bool Ready {
        get {
            _ = EnsureReady();
            return true;
        }
    }

    public string Key =>
        Definition.Key;

    public EntryDataAlignment Alignment =>
        Definition.Alignment;

    public IEntry Entry { get; }
    public IEntryDataDefinition Definition { get; }
    public CancellationToken Token { get; }

    public EntryData(IEntry entry, IEntryDataDefinition definition, CancellationToken token)
        : this(entry, definition, TimeProvider.System, token) {
    }

    Task IEntryData.Ready => EnsureReady();

    async Task IEntryData.Refresh(CancellationToken token) {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task previous;
        lock (PublicationLock) {
            lock (StateLock) {
                previous = RefreshBarrier;
                RefreshBarrier = completion.Task;
                if (Load is not null) {
                    StopProgress(Load);
                }
                Load = null;
            }
            NotifyPropertyChanged(ReadyEvent);
        }
        try {
            try {
                await previous.WaitAsync(token).ConfigureAwait(false);
            }
            catch (Exception) when (!token.IsCancellationRequested) {
                /*
                 * A failed refresh must not prevent a subsequent refresh from retrying.
                 */
            }
            token.ThrowIfCancellationRequested();
            await Definition.RefreshValue(Entry, token).ConfigureAwait(false);
            completion.TrySetResult();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            _ = CompleteCanceledRefreshBarrier(previous, completion);
            throw;
        }
        catch (Exception ex) {
            completion.TrySetException(ex);
            _ = completion.Task.Exception;
            throw;
        }
    }

    int IEntryData.Compare(IEntryData other) {
        /*
         * Null compares lower than any non-null entry data, so mixed sorts
         * keep a consistent order.
         */
        if (other is null) {
            return 1;
        }
        try {
            if (other is EntryData entryData) {
                return Definition.CompareValue(Entry, entryData.Entry);
            }
            return 0;
        }
        catch {
            return 0;
        }
    }

    object ICommandSourceObject.Instance =>
        Entry;

    IEnumerable ICommandSourceObject.Collection =>
        (Entry as ICommandSourceObject)?.Collection;
}
