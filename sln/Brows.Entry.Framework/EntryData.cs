using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class EntryData : Notifier {
        private static readonly ILog Log = Logging.For(typeof(EntryData));

        private static readonly PropertyChangedEventArgs AccessingEventArgs = new(nameof(Accessing));
        private static readonly PropertyChangedEventArgs AccessingFlagEventArgs = new(nameof(AccessingFlag));
        private static readonly PropertyChangedEventArgs AccessCanceledEventArgs = new(nameof(AccessCanceled));
        private static readonly PropertyChangedEventArgs AccessErrorEventArgs = new(nameof(AccessError));
        private static readonly PropertyChangedEventArgs AccessExceptionEventArgs = new(nameof(AccessException));
        private static readonly PropertyChangedEventArgs AlignmentEventArgs = new(nameof(Alignment));
        private static readonly PropertyChangedEventArgs ConverterEventArgs = new(nameof(Converter));
        private static readonly PropertyChangedEventArgs ConverterParameterEventArgs = new(nameof(ConverterParameter));
        private static readonly PropertyChangedEventArgs[] DisplayEventArgs = new PropertyChangedEventArgs[] {
            new(nameof(Display))
        };

        private EntryData(string key, CancellationToken cancellationToken) {
            Key = key;
            CancellationToken = cancellationToken;
        }

        internal abstract object GetValue();

        protected virtual void Refresh() {
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

        public EntryDataAlignment Alignment {
            get => _Alignment;
            set => Change(ref _Alignment, value, AlignmentEventArgs);
        }
        private EntryDataAlignment _Alignment;

        public IEntryDataConverter Converter {
            get => _Converter;
            set => Change(ref _Converter, value, ConverterEventArgs, DisplayEventArgs);
        }
        private IEntryDataConverter _Converter;

        public object ConverterParameter {
            get => _ConverterParameter;
            set => Change(ref _ConverterParameter, value, ConverterParameterEventArgs, DisplayEventArgs);
        }
        private object _ConverterParameter;

        public object Display {
            get {
                var value = GetValue();
                var converter = Converter;
                return converter == null
                    ? value
                    : converter.Convert(value, ConverterParameter, CultureInfo.CurrentUICulture);
            }
        }

        public string Key { get; }
        public CancellationToken CancellationToken { get; }

        public abstract class Of<T> : EntryData, IEntryData, IEntryData<T> {
            private static readonly PropertyChangedEventArgs ReadyEventArgs = new(nameof(Ready));
            private static readonly PropertyChangedEventArgs ValueEventArgs = new(nameof(Value));

            private Task<T> ReadyTask;

            private async Task<T> AccessValue(CancellationToken cancellationToken) {
                Accessing = true;
                AccessError = false;
                AccessCanceled = false;
                AccessException = null;
                _ = Task.Delay(100, cancellationToken).ContinueWith(task => {
                    if (task.IsCompletedSuccessfully) {
                        if (Accessing) {
                            AccessingFlag = true;
                        }
                    }
                }, cancellationToken);
                try {
                    return await Access(cancellationToken);
                }
                catch (Exception ex) {
                    if (ex is OperationCanceledException canceled && canceled.CancellationToken == cancellationToken) {
                        if (Log.Info()) {
                            Log.Info(nameof(OperationCanceledException));
                        }
                        AccessCanceled = true;
                    }
                    else {
                        if (Log.Warn()) {
                            Log.Warn(ex ?? nameof(Exception) as object);
                        }
                        AccessError = true;
                        AccessException = ex;
                    }
                    return default;
                }
                finally {
                    Accessing = false;
                    AccessingFlag = false;
                }
            }

            private async void ReadyInit(CancellationToken cancellationToken) {
                try {
                    Set(await (ReadyTask = AccessValue(cancellationToken)));
                }
                catch {
                    Set(default);
                }
            }

            internal sealed override object GetValue() {
                return Value;
            }

            protected Of(string key, CancellationToken cancellationToken) : base(key, cancellationToken) {
            }

            protected abstract Task<T> Access(CancellationToken cancellationToken);

            protected void Set(T value) {
                Value = value;
            }

            public T Value {
                get => _Value;
                private set => Change(ref _Value, value, ValueEventArgs, DisplayEventArgs);
            }
            private T _Value;

            public bool Ready {
                get {
                    if (ReadyTask == null) {
                        ReadyInit(CancellationToken);
                    }
                    return _Ready = true;
                }
                private set {
                    Change(ref _Ready, value, ReadyEventArgs);
                }
            }
            private bool _Ready;

            object IEntryData.Value =>
                Value;

            Task IEntryData.Ready {
                get {
                    if (ReadyTask == null) {
                        ReadyInit(CancellationToken);
                    }
                    return ReadyTask;
                }
            }

            void IEntryData.Refresh() {
                Refresh();
                ReadyTask = null;
                Ready = false;
            }

            int IEntryData.Compare(IEntryData other) {
                try {
                    return other is IEntryData<T> t
                        ? Comparer<T>.Default.Compare(Value, t.Value)
                        : Comparer.Default.Compare(Value, other?.Value);
                }
                catch {
                    return 0;
                }
            }
        }
    }

    public abstract class EntryData<TValue> : EntryData.Of<TValue> {
        protected EntryData(string key, CancellationToken cancellationToken) : base(key, cancellationToken) {
        }
    }
}
