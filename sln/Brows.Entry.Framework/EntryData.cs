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

        public string ValueFormat =>
            null;

        public object Value {
            get {
                if (Accessed == false) {
                    Accessed = true;
                    Accessing = true;
                    Access().ContinueWith(task => {
                        if (task.IsCanceled) {
                            if (Log.Info()) {
                                Log.Info(nameof(task.IsCanceled));
                            }
                            Value = null;
                        }
                        if (task.IsFaulted) {
                            if (Log.Warn()) {
                                Log.Warn(task.Exception ?? nameof(task.IsFaulted) as object);
                            }
                            Value = null;
                        }
                        if (task.IsCompletedSuccessfully) {
                            Value = task.Result;
                        }
                        Accessing = false;
                    });
                }
                return _Value;
            }
            protected set {
                Change(ref _Value, value, nameof(Value));
            }
        }
        private object _Value;

        public string Key { get; }

        public EntryData(string key) {
            Key = key;
        }

        public virtual void Refresh() {
            Accessed = false;
            NotifyPropertyChanged(nameof(Value));
        }
    }
}
