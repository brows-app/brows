using Domore.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Globalization {
    internal class Dialect : NormallyIndexedItem<Dialect> {
        private readonly Dictionary<string, string> Dictionary = new Dictionary<string, string>();
        private readonly Dictionary<string, HashSet<string>> Lookup = new Dictionary<string, HashSet<string>>();

        public string Name => Index;

        public string Value(string key, string value = null) {
            lock (Dictionary) {
                if (value != null) {
                    Dictionary[key] = value;
                    return value;
                }
                if (Dictionary.TryGetValue(key, out value)) {
                    return value;
                }
                return key;
            }
        }

        public string[] Alias(string key, params string[] values) {
            lock (Lookup) {
                if (Lookup.TryGetValue(key, out var list) == false) {
                    Lookup[key] = list = new HashSet<string>();
                }
                foreach (var value in values) {
                    list.Add(value);
                }
                return list.ToArray();
            }
        }
    }
}
