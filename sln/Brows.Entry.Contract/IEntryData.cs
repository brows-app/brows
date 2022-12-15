using System.Threading.Tasks;

namespace Brows {
    public interface IEntryData {
        string Key { get; }
        object Value { get; }
        Task Ready { get; }
        void Refresh();
        int Compare(IEntryData other);
    }

    public interface IEntryData<T> : IEntryData {
        new T Value { get; }
    }
}
