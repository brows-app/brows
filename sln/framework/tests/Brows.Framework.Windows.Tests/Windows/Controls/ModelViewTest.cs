namespace Brows.Windows.Controls;

[TestFixture]
internal sealed class ModelViewTest {
    [Test]
    public void Constructor_LayoutWrapper_IsNotFocusableOrTabStop() {
        DispatcherTools.Run(dispatcher => {
            var view = new TestView();

            Assert.That(view.Focusable, Is.False);
            Assert.That(view.IsTabStop, Is.False);
        });
    }

    private sealed class TestView : ModelView<object> {
    }
}
