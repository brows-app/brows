using Brows.Composition;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IPC;

[TestFixture]
internal sealed class ObjectPostDelegateTest {
    internal sealed class TestObjectPostDelegate : ObjectPostDelegate {
        private readonly bool Subscribed;

        public int SubscribeCallCount { get; private set; }
        public CancellationToken SubscribeToken { get; private set; }
        public override IObjectTypeResolver TypeResolver { get; } = ObjectTypeResolver.Lookup(typeof(string));

        public TestObjectPostDelegate() : this(subscribed: true) {
        }

        public TestObjectPostDelegate(bool subscribed) {
            Subscribed = subscribed;
        }

        public Task Write(object obj, CancellationToken token) {
            return Post(obj, token);
        }

        public void Finish(Exception error = null) {
            Complete(error);
        }

        protected sealed override Task<bool> Subscribe(CancellationToken token) {
            SubscribeCallCount++;
            SubscribeToken = token;
            return Task.FromResult(Subscribed);
        }

    }

    [Test]
    public async Task Posts_Subscribed_YieldsPostedObjects() {
        var subject = new TestObjectPostDelegate();
        using var cancellation = new CancellationTokenSource();
        var posts = ((IObjectPostDelegate)subject).Posts(cancellation.Token).GetAsyncEnumerator(cancellation.Token);

        try {
            await subject.Write("first", cancellation.Token);
            await subject.Write("second", cancellation.Token);
            subject.Finish();

            var first = await posts.MoveNextAsync();
            var firstItem = posts.Current;
            var second = await posts.MoveNextAsync();
            var secondItem = posts.Current;
            var done = await posts.MoveNextAsync();

            using (Assert.EnterMultipleScope()) {
                Assert.That(first, Is.True);
                Assert.That(firstItem, Is.EqualTo("first"));
                Assert.That(second, Is.True);
                Assert.That(secondItem, Is.EqualTo("second"));
                Assert.That(done, Is.False);
                Assert.That(subject.SubscribeCallCount, Is.EqualTo(1));
                Assert.That(subject.SubscribeToken, Is.EqualTo(cancellation.Token));
            }
        }
        finally {
            await posts.DisposeAsync();
        }
    }

    [Test]
    public async Task Posts_NotSubscribed_YieldsNoObjects() {
        var subject = new TestObjectPostDelegate(subscribed: false);
        using var cancellation = new CancellationTokenSource();
        var actual = new List<object>();

        await foreach (var post in ((IObjectPostDelegate)subject).Posts(cancellation.Token).WithCancellation(cancellation.Token)) {
            actual.Add(post);
        }

        using (Assert.EnterMultipleScope()) {
            Assert.That(actual, Is.Empty);
            Assert.That(subject.SubscribeCallCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void Posts_CompletedWithError_ThrowsError() {
        var subject = new TestObjectPostDelegate();
        var expected = new InvalidOperationException("failed");

        subject.Finish(expected);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in ((IObjectPostDelegate)subject).Posts(CancellationToken.None)) {
            }
        });

        Assert.That(exception, Is.SameAs(expected));
    }

    private sealed class FactoryTestDependency : IExport {
    }

    private sealed class FactoryTestSubject : ObjectPostDelegate {
        [ImportRequired]
        internal FactoryTestDependency Dependency { get; set; }

        public override IObjectTypeResolver TypeResolver =>
            throw new NotSupportedException();

        protected override Task<bool> Subscribe(CancellationToken token) {
            throw new NotSupportedException();
        }

        public sealed class Factory : Factory<FactoryTestSubject> {
        }
    }

    [Test]
    public async Task Factory_ConstructsObjectWithDependencies() {
        using var sandbox = ImportSandbox.Create(
            inject: () => [
                new FactoryTestDependency(),
                new FactoryTestSubject.Factory()]);
        var obj = default(IObjectPostDelegate);
        await sandbox.Enter(sandbox => {
            var factory = sandbox.Imports.Find<IObjectPostFactory>();
            obj = factory.Create();
        });
        Assert.That(obj,
            Is.InstanceOf<FactoryTestSubject>().With
                .Property(nameof(FactoryTestSubject.Dependency)).Not.Null);
    }
}
