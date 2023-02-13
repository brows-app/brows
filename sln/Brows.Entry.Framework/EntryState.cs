using Domore.Notification;
using System.Collections.Generic;

namespace Brows {
    internal class EntryState : Notifier {
        private readonly Dictionary<string, object> State = new();

        private string Key<T>() {
            return typeof(T).Name;
        }

        public object this[string key] =>
            State.TryGetValue(key, out var value)
                ? value
                : null;

        public T Set<T>(T state) {
            var key = Key<T>();
            if (State.ContainsKey(key)) {
                var newValue = state;
                var oldValue = State[key];
                if (Equals(oldValue, newValue)) {
                    return state;
                }
            }
            NotifyPropertyChanged();
            return state;
        }

        public T Get<T>() {
            if (State.TryGetValue(Key<T>(), out var value)) {
                if (value is T t) {
                    return t;
                }
            }
            return default;
        }
    }
}
