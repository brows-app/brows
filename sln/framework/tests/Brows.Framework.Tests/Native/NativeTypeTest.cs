using System;
using System.Threading;

namespace Brows.Native;

[TestFixture]
internal sealed class NativeTypeTest {
    [Test]
    public void Handle_ConcurrentAccess_CreatesOnce() {
        using var native = new TestNativeType { BlockInCreate = true };
        Exception errorA = null;
        Exception errorB = null;

        var threadA = new Thread(() => {
            try {
                native.GetHandle();
            }
            catch (Exception ex) {
                errorA = ex;
            }
        });
        threadA.Start();
        Assert.That(native.FirstCreateEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);

        var threadB = new Thread(() => {
            try {
                native.GetHandle();
            }
            catch (Exception ex) {
                errorB = ex;
            }
        });
        threadB.Start();

        /*
         * In the unsynchronized implementation, the second thread enters Create
         * while the first is still inside it. Under synchronization, the second
         * thread blocks on the lock and never creates a second handle.
         */
        SpinWait.SpinUntil(() => native.CreateCount > 1, TimeSpan.FromSeconds(1));
        native.ReleaseFirstCreate.Set();

        Assert.That(threadA.Join(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(threadB.Join(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(errorA, Is.Null);
        Assert.That(errorB, Is.Null);
        Assert.That(native.CreateCount, Is.EqualTo(1));
    }

    [Test]
    public void Dispose_ConcurrentCalls_DestroysOnce() {
        var native = new TestNativeType { BlockInDestroy = true };
        native.GetHandle();

        var threadA = new Thread(native.Dispose);
        threadA.Start();
        Assert.That(native.FirstDestroyEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);

        var threadB = new Thread(native.Dispose);
        threadB.Start();

        /*
         * In the unsynchronized implementation, both disposals observe the same
         * non-zero handle and each calls Destroy.
         */
        SpinWait.SpinUntil(() => native.DestroyCount > 1, TimeSpan.FromSeconds(1));
        native.ReleaseFirstDestroy.Set();

        Assert.That(threadA.Join(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(threadB.Join(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(native.DestroyCount, Is.EqualTo(1));
    }

    [Test]
    public void Init_Throws_DoesNotPublishHandle() {
        var native = new TestNativeType { ThrowInInit = true };

        Assert.That(() => native.GetHandle(), Throws.TypeOf<InvalidOperationException>());
        Assert.That(native.CreateCount, Is.EqualTo(1));
        Assert.That(native.InitCount, Is.EqualTo(1));
        Assert.That(native.DestroyCount, Is.EqualTo(1));

        /*
         * A later access must start over: create again, run Init again, and
         * succeed now that ThrowInInit no longer applies.
         */
        native.ThrowInInit = false;
        Assert.That(native.GetHandle(), Is.EqualTo(new IntPtr(2)));
        Assert.That(native.CreateCount, Is.EqualTo(2));
        Assert.That(native.InitCount, Is.EqualTo(2));
        Assert.That(native.DestroyCount, Is.EqualTo(1));
    }

    private sealed class TestNativeType : NativeType {
        private int _CreateCount;
        private int _DestroyCount;
        private int _InitCount;

        internal int CreateCount => Volatile.Read(ref _CreateCount);
        internal int DestroyCount => Volatile.Read(ref _DestroyCount);
        internal int InitCount => Volatile.Read(ref _InitCount);
        internal bool BlockInCreate { get; init; }
        internal bool BlockInDestroy { get; init; }
        internal bool ThrowInInit { get; set; }
        internal ManualResetEventSlim FirstCreateEntered { get; } = new(false);
        internal ManualResetEventSlim FirstDestroyEntered { get; } = new(false);
        internal ManualResetEventSlim ReleaseFirstCreate { get; } = new(false);
        internal ManualResetEventSlim ReleaseFirstDestroy { get; } = new(false);

        protected override IntPtr Create() {
            var count = Interlocked.Increment(ref _CreateCount);
            if (BlockInCreate && count == 1) {
                FirstCreateEntered.Set();
                ReleaseFirstCreate.Wait();
            }
            return new IntPtr(count);
        }

        protected override void Destroy(IntPtr handle) {
            var count = Interlocked.Increment(ref _DestroyCount);
            if (BlockInDestroy && count == 1) {
                FirstDestroyEntered.Set();
                ReleaseFirstDestroy.Wait();
            }
        }

        protected override void Init(IntPtr handle) {
            var count = Interlocked.Increment(ref _InitCount);
            if (ThrowInInit && count == 1) {
                throw new InvalidOperationException();
            }
        }
    }
}
