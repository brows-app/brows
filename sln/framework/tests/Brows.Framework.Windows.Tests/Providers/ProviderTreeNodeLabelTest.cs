using System.Windows.Controls;

namespace Brows.Providers;

[TestFixture]
internal sealed class ProviderTreeNodeLabelTest {
    [Test]
    public void ViewModel_IsPresentedAsTheStyledLabelContent() {
        DispatcherTools.Run(dispatcher => {
            var node = Mock.Of<ProviderNavigationNode>();
            var control = new ProviderTreeNodeLabel {
                ViewModel = node
            };
            ProviderTestResources.MergeInto(control);
            control.ApplyTemplate();
            DispatcherTools.Pump(dispatcher);

            var label = (Label)control.Template.FindName("PART_Label", control);
            Assert.That(label.Content, Is.SameAs(node));
            Assert.That(label.ContentTemplate, Is.Not.Null);
        });
    }
}
