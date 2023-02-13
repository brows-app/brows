using System.Collections.Generic;

namespace Brows {
    public interface IEntryProvided {
        IReadOnlyCollection<string> IDs { get; }
        IReadOnlyCollection<string> Names { get; }
        ISet<string> IDSet();
        ISet<string> NameSet();
    }

    public interface IEntryProvided<TEntry> : IEntryProvided {
        IReadOnlyCollection<TEntry> All { get; }
        bool HasID(string id, out TEntry entry);
        bool HasName(string name, out TEntry entry);
    }
}
