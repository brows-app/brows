using System.Windows.Controls;
using System.Windows.Media;

namespace Brows.Providers;

[TestFixture]
internal sealed class ProviderTreeIconImageTest {
    [Test]
    public void Source_AndStretchProperties_AreForwardedToTheStyledImage() {
        DispatcherTools.Run(dispatcher => {
            var source = new DrawingImage {
                Drawing = new GeometryDrawing()
            };
            var control = new ProviderTreeIconImage {
                Source = source,
                Stretch = Stretch.Fill,
                StretchDirection = StretchDirection.Both
            };
            ProviderTestResources.MergeInto(control);
            control.ApplyTemplate();

            var image = (Image)control.Template.FindName("PART_Image", control);
            Assert.That(image.Source, Is.SameAs(source));
            Assert.That(image.Stretch, Is.EqualTo(Stretch.Fill));
            Assert.That(image.StretchDirection, Is.EqualTo(StretchDirection.Both));
        });
    }
}
