using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Providers;

[TestFixture]
internal sealed class ProviderNavigationNodeTest {
    [Test]
    public void Icon_NullResult_RaisesNoPropertyChanged() {
        var node = new TestNode { IconValue = null };
        var notifyCount = 0;
        node.PropertyChanged += (sender, e) => {
            if (e.PropertyName == nameof(ProviderNavigationNode.Icon)) {
                Interlocked.Increment(ref notifyCount);
            }
        };

        /*
         * Reading Icon twice must re-use the first load, not re-enter it.
         * A null icon means the load is complete, not that it never happened.
         */
        Assert.That(node.Icon, Is.Null);
        Assert.That(node.Icon, Is.Null);
        Assert.That(node.GetIconCount, Is.EqualTo(1));
        Assert.That(notifyCount, Is.EqualTo(0));
    }

    [Test]
    public void Icon_Result_IsCachedAndRaisedOnce() {
        var icon = new object();
        var node = new TestNode { IconValue = icon };
        var notifyCount = 0;
        node.PropertyChanged += (sender, e) => {
            if (e.PropertyName == nameof(ProviderNavigationNode.Icon)) {
                Interlocked.Increment(ref notifyCount);
            }
        };

        Assert.That(node.Icon, Is.SameAs(icon));
        Assert.That(node.Icon, Is.SameAs(icon));
        Assert.That(node.GetIconCount, Is.EqualTo(1));
        Assert.That(notifyCount, Is.EqualTo(1));
    }

    private sealed class TestNode : ProviderNavigationNode {
        private int _GetIconCount;

        internal int GetIconCount =>
            Volatile.Read(ref _GetIconCount);

        internal object IconValue { get; init; }

        protected override Task<object> GetIcon(CancellationToken token) {
            Interlocked.Increment(ref _GetIconCount);
            return Task.FromResult(IconValue);
        }

        protected override IAsyncEnumerable<ProviderNavigationNode> EnumerateChildren(CancellationToken token) {
            return Empty();
            static async IAsyncEnumerable<ProviderNavigationNode> Empty() {
                await Task.CompletedTask;
                yield break;
            }
        }

        protected internal override bool PartOf(string id) {
            return false;
        }

        public override string ID =>
            nameof(TestNode);

        public override string Name =>
            nameof(TestNode);
    }
}
