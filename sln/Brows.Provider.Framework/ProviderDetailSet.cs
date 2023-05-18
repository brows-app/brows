using Domore.Notification;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    public sealed class ProviderDetailSet : Notifier {
        private readonly Dictionary<object, ProviderDetail> Set = new();
        private List<ProviderDetail> List = new() { default(ProviderDetail) };

        internal IEnumerable<object> Keys =>
            Set.Keys;

        public ProviderDetail this[object key] {
            get => Set.TryGetValue(key, out var value) ? value : null;
            set {
                var newValue = value;
                var oldValue = Set.TryGetValue(key, out var v) ? v : null;
                if (oldValue != newValue) {
                    Set[key] = value;
                    // The below is temporary until the GUI changes.
                    var list = List = Set.Values.Where(v => v != null).ToList();
                    if (list.Count == 0) {
                        list.Add(default(ProviderDetail));
                    }
                    // ***
                    NotifyPropertyChanged();
                }
            }
        }

        public object At => List;
    }
}
