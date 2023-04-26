using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Brows {
    public static class EntrySorting {
        private sealed class Implementation : IEntrySorting {
            public IReadOnlyDictionary<string, EntrySortDirection?> Agent { get; }

            public Implementation(IReadOnlyDictionary<string, EntrySortDirection?> agent) {
                Agent = agent ?? throw new ArgumentNullException(nameof(agent));
            }

            public IEntrySorting Change(string key, EntrySortDirection? value) {
                var
                agent = Agent.ToDictionary(pair => pair.Key, pair => pair.Value);
                agent[key] = value;
                return From(agent);
            }

            public EntrySortDirection? this[string key] => Agent[key];
            public int Count => Agent.Count;
            public IEnumerable<string> Keys => Agent.Keys;
            public IEnumerable<EntrySortDirection?> Values => Agent.Values;
            public bool ContainsKey(string key) => Agent.ContainsKey(key);
            public bool TryGetValue(string key, [MaybeNullWhen(false)] out EntrySortDirection? value) => Agent.TryGetValue(key, out value);
            public IEnumerator<KeyValuePair<string, EntrySortDirection?>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, EntrySortDirection?>>)Agent).GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Agent).GetEnumerator();
        }

        public static readonly IEntrySorting None = null;
        public static readonly IEntrySorting Empty = new Implementation(new Dictionary<string, EntrySortDirection?>());

        public static IEntrySorting From(IReadOnlyDictionary<string, EntrySortDirection?> sorting) {
            return
                sorting == null ? None :
                sorting.Count == 0 ? Empty :
                new Implementation(sorting);
        }

        public static IEntrySorting From(IReadOnlyDictionary<string, EntrySortDirection> sorting) {
            return From(sorting?.ToDictionary(s => s.Key, s => (EntrySortDirection?)s.Value));
        }

        public static IEntrySorting From(IEnumerable<KeyValuePair<string, EntrySortDirection>> sorting) {
            var dictionary = new Dictionary<string, EntrySortDirection?>();
            foreach (var pair in sorting) {
                dictionary[pair.Key] = pair.Value;
            }
            return From(dictionary);
        }
    }
}
