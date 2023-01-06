using Domore.Runtime.InteropServices;
using Domore.Runtime.Win32;
using System;
using System.Threading;

namespace Brows.IO {
    internal class Win32ProgressSink : FileOperationProgressSink {
        public IOperationProgress Progress { get; }
        public CancellationToken CancellationToken => Progress.CancellationToken;

        public Win32ProgressSink(IOperationProgress progress) {
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }

        public sealed override HRESULT UpdateProgress(uint iWorkTotal, uint iWorkSoFar) {
            if (CancellationToken.IsCancellationRequested) {
                return (HRESULT)winerror.ERROR_CANCELLED;
            }
            Progress.Target.Set(iWorkTotal);
            Progress.Set(iWorkSoFar);
            return base.UpdateProgress(iWorkTotal, iWorkSoFar);
        }
    }
}
