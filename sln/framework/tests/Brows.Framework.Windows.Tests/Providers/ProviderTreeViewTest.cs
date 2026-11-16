using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Brows.Providers;

[TestFixture]
internal sealed class ProviderTreeViewTest {
    [Test]
    public void Current_UsesTheInnerTreeCollectionView() {
        DispatcherTools.Run(dispatcher => {
            var child = new TestNode();
            var root = new TestNode(child);
            root.Expand = true;
            var navigation = new Mock<ProviderNavigation>(root).Object;
            var control = new ProviderTreeView {
                ViewModel = navigation
            };
            ProviderTestResources.MergeInto(control);
            var window = new Window {
                Content = control,
                ShowActivated = false,
                ShowInTaskbar = false
            };
            try {
                window.Show();
                window.UpdateLayout();
                DispatcherTools.Pump(dispatcher);

                var tree = (TreeView)control.Template.FindName("PART_Tree", control);
                tree.UpdateLayout();
                var item = tree.ItemContainerGenerator.ContainerFromItem(child);
                Assert.That(item, Is.TypeOf<ProviderTreeViewItem>());

                var controller = (IProviderNavigationController)control;
                Assert.That(controller.Current(child), Is.True);
                Assert.That(controller.Current(), Is.SameAs(child));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void ViewModel_Loaded_AttachesControllerForProviderFocus() {
        DispatcherTools.Run(dispatcher => {
            var navigation = new Mock<ProviderNavigation>(Mock.Of<ProviderNavigationNode>()).Object;
            var control = new ProviderTreeView {
                ViewModel = navigation
            };
            ProviderTestResources.MergeInto(control);
            var window = new Window {
                Content = control,
                ShowActivated = false,
                ShowInTaskbar = false
            };
            try {
                window.Show();
                window.UpdateLayout();

                Assert.That(((IProviderFocus)navigation).Set(), Is.True);
            }
            finally {
                window.Close();
            }
        });
    }

    private sealed class TestNode : ProviderNavigationNode {
        private IReadOnlyList<ProviderNavigationNode> ChildNodes { get; }

        internal TestNode(params ProviderNavigationNode[] childNodes) {
            ChildNodes = childNodes;
        }

        protected override Task<object> GetIcon(CancellationToken token) {
            return Task.FromResult<object>(null);
        }

        protected override async IAsyncEnumerable<ProviderNavigationNode> EnumerateChildren(
            [EnumeratorCancellation] CancellationToken token) {
            await Task.CompletedTask;
            foreach (var child in ChildNodes) {
                yield return child;
            }
        }

        protected internal override bool PartOf(string id) {
            return false;
        }

        public override string ID => nameof(TestNode);

        public override string Name => nameof(TestNode);
    }
}
