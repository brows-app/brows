using System;
using System.Collections.Generic;

namespace Brows {
    internal abstract class EntryProvided : IEntryProvided {
        public bool CaseSensitive { get; }
        public StringComparer KeyComparer { get; }

        public abstract IReadOnlyCollection<string> IDs { get; }
        public abstract IReadOnlyCollection<string> Names { get; }

        public EntryProvided(bool caseSensitive) {
            CaseSensitive = caseSensitive;
            KeyComparer = CaseSensitive
                ? StringComparer.Ordinal
                : StringComparer.OrdinalIgnoreCase;
        }

        public abstract ISet<string> IDSet();
        public abstract ISet<string> NameSet();
    }

    internal sealed class EntryProvided<TEntry> : EntryProvided, IEntryProvided<TEntry> where TEntry : class, IEntry {
        private readonly Dictionary<string, TEntry> ID;
        private readonly Dictionary<string, TEntry> Name;

        public IReadOnlyCollection<TEntry> All =>
            ID.Values;

        public sealed override IReadOnlyCollection<string> IDs => ID.Keys;
        public sealed override IReadOnlyCollection<string> Names => Name.Keys;

        public EntryProvided(bool caseSensitive) : base(caseSensitive) {
            ID = new(KeyComparer);
            Name = new(KeyComparer);
        }

        public sealed override ISet<string> IDSet() {
            return new HashSet<string>(ID.Keys, KeyComparer);
        }

        public sealed override ISet<string> NameSet() {
            return new HashSet<string>(Name.Keys, KeyComparer);
        }

        public void Add(IReadOnlyList<TEntry> entries) {
            if (null == entries) throw new ArgumentNullException(nameof(entries));
            foreach (var entry in entries) {
                if (entry != null) {
                    ID[entry.ID] = entry;
                    Name[entry.Name] = entry;
                }
            }
        }

        public void Remove(IReadOnlyList<TEntry> entries) {
            if (null == entries) throw new ArgumentNullException(nameof(entries));
            foreach (var entry in entries) {
                if (entry != null) {
                    var id = entry.ID;
                    if (ID.TryGetValue(id, out var ided) && ided == entry) {
                        ID.Remove(id);
                    }
                    var name = entry.Name;
                    if (Name.TryGetValue(name, out var named) && named == entry) {
                        Name.Remove(name);
                    }
                }
            }
        }

        public bool HasID(string id, out TEntry entry) {
            if (ID.TryGetValue(id, out var value)) {
                if (value != null) {
                    entry = value;
                    return true;
                }
            }
            entry = null;
            return false;
        }

        public bool HasName(string name, out TEntry entry) {
            if (Name.TryGetValue(name, out var value)) {
                if (value != null) {
                    entry = value;
                    return true;
                }
            }
            entry = null;
            return false;
        }
    }
}
