using Brows.Operations;
using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Brows.SevenZip;

internal sealed class SevenZipNativeProgress : IDisposable {
    private const int E_ABORT = unchecked((int)0x80004004);
    private const int E_FAIL = unchecked((int)0x80004005);
    private const int S_OK = 0;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ProgressCallback(IntPtr state, ulong completed, ulong total);

    private static readonly ProgressCallback Callback = Report;
    private static readonly IntPtr CallbackPointer = Marshal.GetFunctionPointerForDelegate(Callback);

    private readonly IOperationProgress Progress;
    private readonly CancellationToken Token;
    private readonly Lock Locker = new();
    private GCHandle Handle;
    private Exception CallbackException;
    private ulong Completed;
    private ulong Total;

    private static int Report(IntPtr state, ulong completed, ulong total) {
        try {
            var handle = GCHandle.FromIntPtr(state);
            if (handle.Target is not SevenZipNativeProgress progress) {
                return E_FAIL;
            }
            return progress.Invoke(completed, total);
        }
        catch {
            return E_FAIL;
        }
    }

    private static long ProgressValue(ulong value) {
        return value > long.MaxValue ? long.MaxValue : (long)value;
    }

    private int Invoke(ulong completed, ulong total) {
        try {
            lock (Locker) {
                if (Volatile.Read(ref CallbackException) is not null) {
                    return E_FAIL;
                }
                Token.ThrowIfCancellationRequested();
                var shouldReportTarget = Progress is not null && Total != total;
                if (shouldReportTarget) {
                    Total = total;
                    Progress.Change(setTarget: ProgressValue(total));
                }
                var shouldReportProgress = Progress is not null && completed > Completed;
                if (shouldReportProgress) {
                    var delta = completed - Completed;
                    Completed = completed;
                    Progress.Change(addProgress: ProgressValue(delta));
                }
                else if (completed > Completed) {
                    Completed = completed;
                }
                Token.ThrowIfCancellationRequested();
            }
            return S_OK;
        }
        catch (OperationCanceledException) when (Token.IsCancellationRequested) {
            return E_ABORT;
        }
        catch (Exception exception) {
            Interlocked.CompareExchange(ref CallbackException, exception, null);
            return E_FAIL;
        }
    }

    internal IntPtr Function => Handle.IsAllocated ? CallbackPointer : IntPtr.Zero;
    internal IntPtr State => Handle.IsAllocated ? GCHandle.ToIntPtr(Handle) : IntPtr.Zero;

    internal SevenZipNativeProgress(IOperationProgress progress, CancellationToken token) {
        Progress = progress;
        Token = token;
        Progress?.Change(kind: OperationProgressKind.FileSize);
        var shouldAllocateHandle = progress is not null || token.CanBeCanceled;
        if (shouldAllocateHandle) {
            Handle = GCHandle.Alloc(this);
        }
    }

    internal void ThrowCallbackException() {
        var exception = Volatile.Read(ref CallbackException);
        if (exception is not null) {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    internal void ThrowIfCancellationRequested() {
        Token.ThrowIfCancellationRequested();
    }

    public void Dispose() {
        if (Handle.IsAllocated) {
            Handle.Free();
        }
    }
}
