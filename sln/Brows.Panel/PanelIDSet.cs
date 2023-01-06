using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    public class PanelIDSet<T> where T : IPanelID {
        private static readonly ILog Log = Logging.For(typeof(PanelIDSet<T>));

        private Dictionary<string, T> Dict = new Dictionary<string, T>();

        private void Item_ValueChanged(object sender, EventArgs e) {
            if (Log.Info()) {
                Log.Info(
                    nameof(Item_ValueChanged),
                    (sender as IPanelID)?.Value);
            }
            var newDict = Dict.Values
                .GroupBy(item => item.Value)
                .ToDictionary(group => group.Key, group => group.First());
            var removed = Dict.Values
                .Except(newDict.Values)
                .ToList();
            Dict = newDict;
            foreach (var item in removed) {
                if (Log.Info()) {
                    Log.Info(
                        nameof(Removed),
                        item?.Value);
                }
                item.ValueChanged -= Item_ValueChanged;
            }
            Removed(removed);
        }

        protected virtual void Removed(IEnumerable<T> items) {
        }

        public IReadOnlyCollection<string> Values =>
            Dict.Keys;

        public T Get(string value) {
            return Dict.TryGetValue(value, out var item)
                ? item
                : default(T);
        }

        public T GetOrAdd(string value, Func<T> factory) {
            if (null == factory) throw new ArgumentNullException(nameof(factory));
            var dict = Dict;
            if (dict.TryGetValue(value, out var item) == false) {
                Add(item = factory());
            }
            return item;
        }

        public bool Add(T item) {
            if (item != null) {
                var key = item.Value;
                if (Dict.ContainsKey(key) == false) {
                    Dict.Add(key, item);
                    if (Log.Info()) {
                        Log.Info(
                            nameof(Add),
                            item.Value);
                    }
                    item.ValueChanged += Item_ValueChanged;
                    return true;
                }
            }
            return false;
        }
    }
}
