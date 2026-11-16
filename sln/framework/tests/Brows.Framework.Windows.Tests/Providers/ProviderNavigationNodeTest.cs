using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Brows.Providers;

[TestFixture]
internal sealed class ProviderNavigationNodeTest {
    [Test]
    public void Icon_ImmediatelyCompletedLoad_UpdatesBinding() {
        DispatcherTools.Run(dispatcher => {
            var icon = new object();
            var node = new TestNode(icon);
            var target = new Border();
            BindingOperations.SetBinding(target, FrameworkElement.TagProperty, new Binding(nameof(node.Icon)) {
                Source = node
            });

            DispatcherTools.Pump(dispatcher);

            Assert.That(target.Tag, Is.SameAs(icon));
        });
    }

    private sealed class TestNode : ProviderNavigationNode {
        private object IconValue { get; }

        internal TestNode(object iconValue) {
            IconValue = iconValue;
        }

        protected override Task<object> GetIcon(CancellationToken token) {
            return Task.FromResult(IconValue);
        }

        protected override async IAsyncEnumerable<ProviderNavigationNode> EnumerateChildren(
            [EnumeratorCancellation] CancellationToken token) {
            await Task.CompletedTask;
            yield break;
        }

        protected internal override bool PartOf(string id) {
            return false;
        }

        public override string ID => nameof(TestNode);
        public override string Name => nameof(TestNode);
    }
}
