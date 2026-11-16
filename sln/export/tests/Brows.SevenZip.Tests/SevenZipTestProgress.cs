using Brows.Operations;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Counts the progress that the plugin reports. The progress is used to check
/// that the work that is done is reported to the caller.
/// </summary>
internal sealed class SevenZipTestProgress : IOperationProgress {
    private readonly Lock Locker = new();
    private readonly SevenZipTestProgress Parent;

    public OperationProgressKind? Kind { get; private set; }

    /// <summary>
    /// Gets the amount of work that was reported as done.
    /// </summary>
    public long Value { get; private set; }

    /// <summary>
    /// Gets the amount of work that was reported as expected.
    /// </summary>
    public long Target { get; private set; }

    public SevenZipTestProgress() {
    }

    private SevenZipTestProgress(SevenZipTestProgress parent) {
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));
    }

    public void Change(long? addProgress = null,
                       long? setProgress = null,
                       long? addTarget = null,
                       long? setTarget = null,
                       string progressString = null,
                       string targetString = null,
                       string name = null,
                       string data = null,
                       OperationProgressKind? kind = null,
                       string progressFormat = null,
                       string targetFormat = null,
                       string progressRateFormat = null,
                       string progressRateAverageFormat = null,
                       System.IFormatProvider formatProvider = null) {
        long progressChange;
        long targetChange;
        lock (Locker) {
            var previousProgress = Value;
            var previousTarget = Target;
            Value = (setProgress ?? Value) + (addProgress ?? 0);
            Target = (setTarget ?? Target) + (addTarget ?? 0);
            Kind = kind ?? Kind;
            progressChange = Value - previousProgress;
            targetChange = Target - previousTarget;
        }
        if (progressChange != 0 || targetChange != 0) {
            Parent?.Change(addProgress: progressChange, addTarget: targetChange);
        }
    }

    public async Task<OperationResult> Child(
        string name,
        OperationDelegate task,
        OperationFailureBehavior failureBehavior,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        try {
            ArgumentNullException.ThrowIfNull(task);
            await task(new SevenZipTestProgress(this), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return new OperationResult(OperationResultKind.Succeeded, null);
        }
        catch (Exception error) {
            cancellationToken.ThrowIfCancellationRequested();
            if (failureBehavior == OperationFailureBehavior.Throw) {
                throw;
            }
            var kind = error is OperationCanceledException ? OperationResultKind.Canceled : OperationResultKind.Failed;
            return new OperationResult(kind, error);
        }
    }

    public Task<OperationBatchResult> Children<T>(
            IEnumerable<T> source,
            Func<T, OperationChild> child,
            OperationFailureBehavior failureBehavior,
            CancellationToken cancellationToken) {
        throw new NotSupportedException();
    }
}
