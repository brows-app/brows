using System.Threading;

namespace Brows {
    public interface IOperationProgress {
        CancellationToken CancellationToken { get; }
        void Target(long value);
        void Progress(long value);
    }
}
