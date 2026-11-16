using Brows.Entries;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Tests;

internal sealed class TestOneShotEntryStreamSet : IEntryStreamSet {
    private sealed class Ready : IEntryStreamReady {
        public bool IsDisposed { get; private set; }

        public void Dispose() {
            IsDisposed = true;
        }
    }

    private readonly IReadOnlyList<IEntryStreamSource> Sources;
    private Ready StreamReadyState;

    private async IAsyncEnumerable<IEntryStreamSource> Enumerate([EnumeratorCancellation] CancellationToken token) {
        EnumerationCount++;
        if (EnumerationCount > 1) {
            throw new InvalidOperationException("This stream set can only be enumerated once.");
        }
        if (ReadinessIsDisposed) {
            throw new InvalidOperationException("The stream set readiness was disposed during materialization.");
        }
        foreach (var source in Sources) {
            token.ThrowIfCancellationRequested();
            yield return source;
        }
        await Task.CompletedTask;
    }

    public int EnumerationCount { get; private set; }
    public bool ReadinessIsDisposed => StreamReadyState?.IsDisposed == true;

    public TestOneShotEntryStreamSet(params IEntryStreamSource[] sources) {
        Sources = sources ?? throw new ArgumentNullException(nameof(sources));
    }

    public IEntryStreamReady StreamSourceReady() {
        StreamReadyState = new Ready();
        return StreamReadyState;
    }

    public IEnumerable<string> FileSource() {
        return Array.Empty<string>();
    }

    public IAsyncEnumerable<IEntryStreamSource> StreamSource(CancellationToken token) {
        return Enumerate(token);
    }
}
