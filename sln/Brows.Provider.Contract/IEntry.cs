using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IEntry {
        event EntryDropEventHandler Drop;
        event EntrySelectedEventHandler Selected;
        event EntryRefreshedEventHandler Refreshed;

        IEntryData this[string key] { get; }

        string ID { get; }
        string Name { get; }
        bool Select { get; set; }

        Task Refresh(CancellationToken token);
    }
}
