using System.Windows;
using System.Windows.Controls;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryColumnMeasureTest {
    [TestCase(0)]
    [TestCase(32)]
    public void Sample_RecordsFullContentAndPreservesConstrainedLayout(double width) {
        DispatcherTools.Run(_ => {
            var measure = new EntryColumnMeasure { Child = new Border { Width = 300, Height = 20 } };
            measure.Measure(new Size(width, 100));
            measure.Sample();
            Assert.That(measure.NaturalWidth, Is.EqualTo(300));
            Assert.That(measure.DesiredSize.Width, Is.LessThanOrEqualTo(width));
            Assert.That(measure.DesiredSize.Height, Is.EqualTo(width == 0 ? 0 : 20));
            Assert.That(measure.ClipToBounds, Is.True);
        });
    }
}
