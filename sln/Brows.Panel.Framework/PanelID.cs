using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using ComponentModel;

    public class PanelID : NotifyPropertyChanged, IPanelID {
        private static readonly PropertyChangedEventArgs ErrorEventArgs = new(nameof(Error));
        private static readonly PropertyChangedEventArgs CanonicalEventArgs = new(nameof(Canonical));
        private static readonly PropertyChangedEventArgs ValueEventArgs = new(nameof(Value));

        protected virtual bool HasCanonicalValue => false;

        protected virtual Task<string> GetCanonicalValue(CancellationToken cancellationToken) {
            return Task.FromResult(Value);
        }

        public event EventHandler ValueChanged;

        public Exception Error {
            get => _Error;
            private set => Change(ref _Error, value, ErrorEventArgs);
        }
        private Exception _Error;

        public bool Canonical {
            get => _Canonical;
            private set => Change(ref _Canonical, value, CanonicalEventArgs);
        }
        private bool _Canonical;

        public string Value {
            get => _Value;
            private set => Change(ref _Value, value, ValueEventArgs);
        }
        private string _Value;

        public PanelID(string value) {
            Value = value;
        }

        public async void Begin(CancellationToken cancellationToken) {
            if (HasCanonicalValue) {
                try {
                    var value = await GetCanonicalValue(cancellationToken);
                    if (Value != value) {
                        Value = value;
                        ValueChanged?.Invoke(this, EventArgs.Empty);
                    }
                    Canonical = true;
                }
                catch (Exception ex) {
                    Error = ex is OperationCanceledException
                        ? Error
                        : ex;
                }
            }
        }
    }
}
