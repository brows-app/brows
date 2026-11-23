using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Brows.Commands;

internal sealed class FindWork {
    private readonly Func<FileSystemInfo, CancellationToken, Task<FindEntryResult>> ProcessEntry;

    private async Task Work(IOperationProgress progress, CancellationToken token) {
        var settings = new FindSettings(Parameter);
        var findIn = new FindInInfo(Root, settings) { Translation = Result.Translation };
        var process = ProcessEntry ?? findIn.Process;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var pipelineToken = cancellation.Token;
        var entries = Channel.CreateBounded<FileSystemInfo>(new BoundedChannelOptions(settings.Workers * 2) {
            SingleReader = settings.Workers == 1,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        var outcomes = Channel.CreateBounded<FindEntryResult>(new BoundedChannelOptions(settings.Workers * 2) {
            SingleReader = true,
            SingleWriter = settings.Workers == 1,
            FullMode = BoundedChannelFullMode.Wait
        });
        Exception failure = null;
        long discovered = 0;
        long traversalErrors = 0;

        void Fail(Exception ex) {
            var expectedCancellation = ex is OperationCanceledException && pipelineToken.IsCancellationRequested;
            var recordFailure = !expectedCancellation && !token.IsCancellationRequested;
            if (recordFailure) {
                Interlocked.CompareExchange(ref failure, ex, null);
            }
            cancellation.Cancel();
        }

        async Task Produce() {
            try {
                var recurse = !settings.In.HasFlag(FindIn.TopDirectoryOnly);
                await foreach (var entry in FindTraversal.Enumerate(
                    Root,
                    recurse,
                    settings.Exclude,
                    () => Interlocked.Increment(ref traversalErrors),
                    pipelineToken)
                    .ConfigureAwait(false)) {
                    Interlocked.Increment(ref discovered);
                    await entries.Writer.WriteAsync(entry, pipelineToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) {
                Fail(ex);
            }
            finally {
                entries.Writer.TryComplete();
            }
        }

        async Task Worker() {
            try {
                await foreach (var entry in entries.Reader.ReadAllAsync(pipelineToken).ConfigureAwait(false)) {
                    FindEntryResult outcome;
                    try {
                        outcome = await process(entry, pipelineToken).ConfigureAwait(false);
                    }
                    catch (RegexMatchTimeoutException ex) {
                        FoundInInfo[] rows = entry is FileInfo file ? [new FoundInFileError(file, Root, ex)] : [];
                        outcome = new FindEntryResult(entry, rows, Tried: true, Errors: 1);
                    }
                    await outcomes.Writer.WriteAsync(outcome, pipelineToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) {
                Fail(ex);
            }
        }

        var producer = Task.Run(Produce, CancellationToken.None);
        var workers = new Task[settings.Workers];
        for (var i = 0; i < workers.Length; i++) {
            workers[i] = Task.Run(Worker, CancellationToken.None);
        }
        async Task FinishWorkers() {
            try {
                await Task.WhenAll(workers).ConfigureAwait(false);
            }
            finally {
                outcomes.Writer.TryComplete();
            }
        }
        var finishing = FinishWorkers();
        var rows = new List<FoundInInfo>(64);
        long processed = 0;
        long tried = 0;
        long matched = 0;
        long errors = 0;
        long skipped = 0;
        var retained = 0;
        string name = null;

        void Flush() {
            if (rows.Count > 0) {
                var batch = rows.ToArray();
                rows.Clear();
                Result.Add(batch);
            }
            Result.MatchTried = tried;
            Result.MatchMatched = matched;
            Result.ErrorCount = errors + Interlocked.Read(ref traversalErrors);
            Result.SkippedCount = skipped;
            progress?.Change(setProgress: processed, setTarget: Interlocked.Read(ref discovered), data: name);
        }

        void Receive(FindEntryResult outcome) {
            pipelineToken.ThrowIfCancellationRequested();
            processed++;
            tried += outcome.Tried ? 1 : 0;
            matched += outcome.Matched ? 1 : 0;
            errors += outcome.Errors;
            skipped += outcome.Skipped;
            Result.Truncated |= outcome.Truncated;
            name = outcome.Info.Name;
            foreach (var row in outcome.Rows) {
                if (retained == settings.MaximumResults) {
                    Result.Truncated = true;
                    break;
                }
                rows.Add(row);
                retained++;
                if (rows.Count == 64) {
                    var batch = rows.ToArray();
                    rows.Clear();
                    Result.Add(batch);
                }
            }
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        var ready = outcomes.Reader.WaitToReadAsync(pipelineToken).AsTask();
        var tick = timer.WaitForNextTickAsync(pipelineToken).AsTask();
        try {
            for (; ; ) {
                await Task.WhenAny(ready, tick).ConfigureAwait(false);
                if (tick.IsCompleted) {
                    await tick.ConfigureAwait(false);
                    Flush();
                    tick = timer.WaitForNextTickAsync(pipelineToken).AsTask();
                }
                if (ready.IsCompleted) {
                    if (!await ready.ConfigureAwait(false)) {
                        break;
                    }
                    /*
                     * Bound each drain so the timer can flush a partial batch even when
                     * other workers are slow or the outcome channel stays continuously ready.
                     */
                    for (var i = 0; i < 16; i++) {
                        if (!outcomes.Reader.TryRead(out var outcome)) {
                            break;
                        }
                        Receive(outcome);
                    }
                    ready = outcomes.Reader.WaitToReadAsync(pipelineToken).AsTask();
                }
            }
        }
        catch (Exception ex) {
            Fail(ex);
        }
        finally {
            timer.Dispose();
            /*
             * Always join every producer/worker before publishing terminal state or releasing
             * resources. A failed consumer cancels writers blocked by backpressure.
             */
            await Task.WhenAll(producer, finishing).ConfigureAwait(false);
            try {
                Flush();
            }
            catch (Exception ex) {
                Fail(ex);
            }
        }
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        token.ThrowIfCancellationRequested();
    }

    internal DirectoryInfo Root { get; }
    internal Find.Parameter Parameter { get; }
    internal FileSystemFindResult Result { get; }

    internal FindWork(FileSystemFindResult result,
        Func<FileSystemInfo, CancellationToken, Task<FindEntryResult>> processEntry = null) {
        Result = result ?? throw new ArgumentNullException(nameof(result));
        Root = Result.Root ?? throw new ArgumentException(nameof(result));
        Parameter = Result.Parameter ?? throw new ArgumentException(nameof(result));
        ProcessEntry = processEntry;
    }

    internal async Task Done(IOperationProgress progress, CancellationToken token) {
        try {
            await Work(progress, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            Result.Canceled = true;
            throw;
        }
        catch (Exception ex) {
            Result.Exception = ex;
            throw;
        }
        finally {
            Result.Complete = true;
        }
    }
}
