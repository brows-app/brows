using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryTaskCollectionTest {
    [Test]
    public async Task Get_FaultedTask_RunsFactoryAgain() {
        var collection = new EntryTaskCollection();
        var count = 0;
        Task<int> Factory(CancellationToken token) {
            count++;
            return count == 1
                ? Task.FromException<int>(new InvalidOperationException())
                : Task.FromResult(count);
        }

        var first = collection.Get("key", Factory, CancellationToken.None);
        Assert.That(first.Status, Is.EqualTo(TaskStatus.Faulted));

        /*
         * The next call must re-run the factory and get the new task's result,
         * rather than being served the cached faulted task forever.
         */
        var second = await collection.Get("key", Factory, CancellationToken.None);
        Assert.That(second, Is.EqualTo(2));
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public async Task Get_CanceledTask_RunsFactoryAgain() {
        var collection = new EntryTaskCollection();
        var count = 0;
        Task<int> Factory(CancellationToken token) {
            count++;
            return count == 1
                ? Task.FromCanceled<int>(new CancellationToken(canceled: true))
                : Task.FromResult(count);
        }

        var first = collection.Get("key", Factory, CancellationToken.None);
        Assert.That(first.Status, Is.EqualTo(TaskStatus.Canceled));

        /*
         * The next call must re-run the factory and get the new task's result,
         * rather than being served the cached canceled task forever.
         */
        var second = await collection.Get("key", Factory, CancellationToken.None);
        Assert.That(second, Is.EqualTo(2));
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public void Get_ConcurrentCalls_RunsFactoryOnce() {
        var collection = new EntryTaskCollection();
        var count = 0;
        var factoryEntered = new ManualResetEventSlim(false);
        var releaseFactory = new ManualResetEventSlim(false);
        Task<int> taskA = null;
        Task<int> taskB = null;
        Task<int> Factory(CancellationToken token) {
            count++;
            factoryEntered.Set();
            releaseFactory.Wait(TimeSpan.FromSeconds(5), CancellationToken.None);
            return Task.FromResult(1);
        }

        var threadA = new Thread(() => taskA = collection.Get("key", Factory, CancellationToken.None));
        threadA.Start();
        Assert.That(factoryEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(count, Is.EqualTo(1));

        var threadB = new Thread(() => taskB = collection.Get("key", Factory, CancellationToken.None));
        threadB.Start();

        /*
         * B must have reached its cache lookup and be waiting on the lock that
         * A still holds inside the factory. A's task is not stored until the
         * factory returns, so B's lookup must have already missed the cache.
         */
        Assert.That(SpinWait.SpinUntil(
            () => threadB.ThreadState == ThreadState.WaitSleepJoin,
            TimeSpan.FromSeconds(2)), Is.True);

        releaseFactory.Set();
        Assert.That(threadA.Join(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(threadB.Join(TimeSpan.FromSeconds(5)), Is.True);

        /*
         * With get-or-add semantics, B finds the task A stored instead of
         * running the factory a second time and discarding A's task.
         */
        Assert.That(count, Is.EqualTo(1));
        Assert.That(taskA.Result, Is.EqualTo(1));
        Assert.That(taskB, Is.SameAs(taskA));
    }
}