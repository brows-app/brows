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
    }
}
