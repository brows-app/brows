using System.Threading;

namespace Brows {
    public interface IOperationProgress {
        CancellationToken CancellationToken { get; }
        IOperationProgressInfo Info { get; }
        IOperationProgressTarget Target { get; }
        long Get();
        void Add(long value);
        void Set(long value);
        IOperable Child(string name);
    }
}
