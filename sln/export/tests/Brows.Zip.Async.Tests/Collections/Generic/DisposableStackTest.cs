using System.IO;

namespace Brows.Collections.Generic;

[TestFixture]
internal sealed class DisposableStackTest {
    [Test]
    public void Dispose_AttemptsAllResourcesAndPropagatesTheFirstFailure() {
        var firstFailure = new InvalidOperationException("First disposal failure.");
        var stack = new DisposableStack();
        var remaining = new TrackingDisposable();
        stack.Push(remaining);
        stack.Push(new ThrowingDisposable(new IOException("Later disposal failure.")));
        stack.Push(new ThrowingDisposable(firstFailure));

        var actual = Assert.Throws<InvalidOperationException>(stack.Dispose);

        using (Assert.EnterMultipleScope()) {
            Assert.That(actual, Is.SameAs(firstFailure));
            Assert.That(remaining.Disposed, Is.True);
            Assert.That(stack.Disposed, Is.True);
        }
    }

    private sealed class TrackingDisposable : IDisposable {
        public bool Disposed { get; private set; }

        public void Dispose() {
            Disposed = true;
        }
    }

    private sealed class ThrowingDisposable : IDisposable {
        private readonly Exception Failure;

        public ThrowingDisposable(Exception failure) {
            Failure = failure ?? throw new ArgumentNullException(nameof(failure));
        }

        public void Dispose() {
            throw Failure;
        }
    }
}
