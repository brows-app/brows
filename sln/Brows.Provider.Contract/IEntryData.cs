using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IEntryData {
        string Key { get; }
        object Value { get; }
        Task Ready { get; }
        Task Refresh(CancellationToken token);
        int Compare(IEntryData other);
    }
}
