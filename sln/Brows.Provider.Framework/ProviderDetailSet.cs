using Domore.Notification;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    public sealed class ProviderDetailSet : Notifier {
        private readonly Dictionary<object, ProviderDetail> Set = [];
        private List<ProviderDetail> List = [null];

        internal IEnumerable<object> Keys =>
            Set.Keys;

        public ProviderDetail this[object key] {
            get => Set.TryGetValue(key, out var value) ? value : null;
            set {
                bool change() => value != (Set.TryGetValue(key, out var v) ? v : null);
                bool changed;
                if (true == (changed = change())) {
                    lock (Set) {
                        if (true == (changed = change())) {
                            Set[key] = value;
                            // The below is temporary until the GUI changes.
                            var list = List = Set.Values.Where(v => v != null).ToList();
                            if (list.Count == 0) {
                                list.Add(null);
                            }
                            // ***
                        }
                    }
                }
                if (changed) {
                    NotifyPropertyChanged();
                }
            }
        }

        public object At => List;
    }
}
