using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Entries;

public interface IEntry {
    event EntryDropEventHandler Drop;
    event EntrySelectedEventHandler Selected;
    event EntryRefreshedEventHandler Refreshed;

    IEntryData this[string key] { get; }

    string ID { get; }
    string Name { get; }
    bool Select { get; set; }

    Task Refresh(CancellationToken token);
    Task<T> Task<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken token);
}
