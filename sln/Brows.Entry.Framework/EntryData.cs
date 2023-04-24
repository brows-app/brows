using Domore.Logs;
using Domore.Notification;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class EntryData : Notifier, IEntryData {
        private static readonly ILog Log = Logging.For(typeof(EntryData));

        private static readonly PropertyChangedEventArgs AccessingEventArgs = new(nameof(Accessing));
        private static readonly PropertyChangedEventArgs AccessingFlagEventArgs = new(nameof(AccessingFlag));
        private static readonly PropertyChangedEventArgs AccessCanceledEventArgs = new(nameof(AccessCanceled));
        private static readonly PropertyChangedEventArgs AccessErrorEventArgs = new(nameof(AccessError));
        private static readonly PropertyChangedEventArgs AccessExceptionEventArgs = new(nameof(AccessException));
        private static readonly PropertyChangedEventArgs[] DisplayEventArgs = new PropertyChangedEventArgs[] { new(nameof(Display)) };
        private static readonly PropertyChangedEventArgs ReadyEventArgs = new(nameof(Ready));
        private static readonly PropertyChangedEventArgs ValueEventArgs = new(nameof(Value));

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
                return await Definition.GetValue(
                    entry: Entry,
                    progress: value => Value = value,
                    cancellationToken: token);
            }
            catch (OperationCanceledException canceled) when (canceled.CancellationToken == token) {
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
            catch (OperationCanceledException canceled) when (canceled.CancellationToken == Token) {
                if (Log.Debug()) {
                    Log.Debug(Log.Join(nameof(ReadyInit), nameof(canceled)));
                }
                Value = null;
            }
        }

        public bool Accessing {
            get => _Accessing;
            private set => Change(ref _Accessing, value, AccessingEventArgs);
        }
        private bool _Accessing;

        public bool AccessingFlag {
            get => _AccessingFlag;
            private set => Change(ref _AccessingFlag, value, AccessingFlagEventArgs);
        }
        private bool _AccessingFlag;

        public bool AccessCanceled {
            get => _AccessCanceled;
            private set => Change(ref _AccessCanceled, value, AccessCanceledEventArgs);
        }
        private bool _AccessCanceled;

        public bool AccessError {
            get => _AccessError;
            private set => Change(ref _AccessError, value, AccessErrorEventArgs);
        }
        private bool _AccessError;

        public Exception AccessException {
            get => _AccessException;
            private set => Change(ref _AccessException, value, AccessExceptionEventArgs);
        }
        private Exception _AccessException;

        public object Value {
            get => _Value;
            private set => Change(ref _Value, value, ValueEventArgs, DisplayEventArgs);
        }
        private object _Value;

        public object Display {
            get {
                var converter = Definition.Converter;
                return converter == null
                    ? Value
                    : converter.Convert(Value, Definition.ConverterParameter, CultureInfo.CurrentUICulture);
            }
        }

        public bool Ready {
            get {
                if (ReadyTask == null) {
                    ReadyInit();
                }
                return _Ready = true;
            }
            private set {
                Change(ref _Ready, value, ReadyEventArgs);
            }
        }
        private bool _Ready;

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

        void IEntryData.Refresh() {
            Definition.RefreshValue(Entry);
            ReadyTask = null;
            Ready = false;
        }

        int IEntryData.Compare(IEntryData other) {
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
    }
}
