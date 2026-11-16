using Brows.Commands;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections;
using System.ComponentModel;
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
    private static readonly PropertyChangedEventArgs[] ValueDependents = new[] { DisplayEvent };

    private Task<object> ReadyTask;

    private async Task<object> AccessValue(CancellationToken token) {
        Accessing = true;
        AccessError = false;
        AccessCanceled = false;
        AccessException = null;
        _ = Task.Delay(100, token).ContinueWith(task => {
            if (task.IsCompletedSuccessfully) {
                if (Accessing) {
                    AccessingFlag = true;
                }
            }
        }, token);
        try {
            return await Definition
                .GetValue(
                    entry: Entry,
                    progress: value => Value = value,
                    token: token);
        }
        catch (OperationCanceledException canceled) when (token.IsCancellationRequested) {
            if (Log.Info()) {
                Log.Info(nameof(canceled));
            }
            AccessCanceled = true;
            return default;
        }
        catch (Exception ex) {
            if (Log.Warn()) {
                Log.Warn(ex ?? nameof(Exception) as object);
            }
            AccessError = true;
            AccessException = ex;
            return default;
        }
        finally {
            Accessing = false;
            AccessingFlag = false;
        }
    }

    private async void ReadyInit() {
        try {
            Value = await (ReadyTask = AccessValue(Token));
        }
        catch (OperationCanceledException canceled) when (Token.IsCancellationRequested) {
            if (Log.Debug()) {
                Log.Debug(Log.Join(nameof(ReadyInit), nameof(canceled)));
            }
            Value = null;
        }
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

    public object Value {
        get;
        private set => Change(ref field, value, ValueEvent, ValueDependents);
    }

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
            if (ReadyTask is null) {
                ReadyInit();
            }
            return field = true;
        }
        private set {
            Change(ref field, value, ReadyEvent);
        }
    }

    public string Key =>
        Definition.Key;

    public EntryDataAlignment Alignment =>
        Definition.Alignment;

    public IEntry Entry { get; }
    public IEntryDataDefinition Definition { get; }
    public CancellationToken Token { get; }

    public EntryData(IEntry entry, IEntryDataDefinition definition, CancellationToken token) {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Token = token;
    }

    Task IEntryData.Ready {
        get {
            if (ReadyTask == null) {
                ReadyInit();
            }
            return ReadyTask;
        }
    }

    async Task IEntryData.Refresh(CancellationToken token) {
        await Definition.RefreshValue(Entry, token);
        ReadyTask = null;
        Ready = false;
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
