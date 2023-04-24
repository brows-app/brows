using Domore.Runtime.InteropServices;
using Domore.Runtime.Win32;
using System.Threading;

namespace Brows {
    internal sealed class Win32ProgressSink : FileOperationProgressSink {
        public IOperationProgress Progress { get; }
        public CancellationToken CancellationToken { get; }

        public Win32ProgressSink(IOperationProgress progress, CancellationToken cancellationToken) {
            Progress = progress;
            CancellationToken = cancellationToken;
        }

        public sealed override HRESULT UpdateProgress(uint iWorkTotal, uint iWorkSoFar) {
            if (CancellationToken.IsCancellationRequested) {
                return (HRESULT)winerror.ERROR_CANCELLED;
            }
            Progress?.Target?.Set(iWorkTotal);
            Progress?.Set(iWorkSoFar);
            return base.UpdateProgress(iWorkTotal, iWorkSoFar);
        }
    }
}
