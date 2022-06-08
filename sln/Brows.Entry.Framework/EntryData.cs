using System;
using System.Globalization;
using System.Threading.Tasks;

namespace Brows {
    using ComponentModel;
    using Logger;

    public abstract class EntryData : NotifyPropertyChanged, IEntryData {
        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(EntryData)));
        private ILog _Log;

        protected abstract Task<object> Access();

        protected void Set(object value) {
            Change(ref _Value, value, nameof(Value), nameof(ConvertedValue));
        }

        public bool Accessed {
            get => _Accessed;
            private set => Change(ref _Accessed, value, nameof(Accessed));
        }
        private bool _Accessed;

        public bool Accessing {
            get => _Accessing;
            private set => Change(ref _Accessing, value, nameof(Accessing));
        }
        private bool _Accessing;

        public bool AccessingFlag {
            get => _AccessingFlag;
            private set => Change(ref _AccessingFlag, value, nameof(AccessingFlag));
        }
        private bool _AccessingFlag;

        public bool AccessCanceled {
            get => _AccessCanceled;
            private set => Change(ref _AccessCanceled, value, nameof(AccessCanceled));
        }
        private bool _AccessCanceled;

        public bool AccessError {
            get => _AccessError;
            private set => Change(ref _AccessError, value, nameof(AccessError));
        }
        private bool _AccessError;

        public Exception AccessException {
            get => _AccessException;
            private set => Change(ref _AccessException, value, nameof(AccessException));
        }
        private Exception _AccessException;

        public object Value {
            get {
                if (Accessed == false) {
                    Accessed = true;
                    Accessing = true;
                    AccessError = false;
                    AccessCanceled = false;
                    AccessException = null;
                    Access().ContinueWith(task => {
                        if (task.IsCanceled) {
                            if (Log.Info()) {
                                Log.Info(nameof(task.IsCanceled));
                            }
                            AccessCanceled = true;
                            Set(null);
                        }
                        if (task.IsFaulted) {
                            if (Log.Warn()) {
                                Log.Warn(task.Exception ?? nameof(task.IsFaulted) as object);
                            }
                            AccessError = true;
                            AccessException = task.Exception;
                            Set(null);
                        }
                        if (task.IsCompletedSuccessfully) {
                            Set(task.Result);
                        }
                        Accessing = false;
                        AccessingFlag = false;
                    });
                    Task.Delay(100).ContinueWith(task => {
                        if (Accessing) {
                            AccessingFlag = true;
                        }
                    });
                }
                return _Value;
            }
        }
        private object _Value;

        public object ConvertedValue {
            get {
                var converter = Converter;
                return converter == null
                    ? Value
                    : converter.Convert(Value, ConverterParameter, CultureInfo.CurrentUICulture);
            }
        }

        public IEntryDataConverter Converter {
            get => _Converter;
            set => Change(ref _Converter, value, nameof(Converter), nameof(ConvertedValue));
        }
        private IEntryDataConverter _Converter;

        public object ConverterParameter {
            get => _ConverterParameter;
            set => Change(ref _ConverterParameter, value, nameof(ConverterParameter), nameof(ConvertedValue));
        }
        private object _ConverterParameter;

        public EntryDataAlignment Alignment {
            get => _Alignment;
            set => Change(ref _Alignment, value, nameof(Alignment));
        }
        private EntryDataAlignment _Alignment;

        public string Key { get; }

        public EntryData(string key) {
            Key = key;
        }

        public virtual void Refresh() {
            Accessed = false;
            NotifyPropertyChanged(nameof(Value));
            NotifyPropertyChanged(nameof(ConvertedValue));
        }
    }
}
