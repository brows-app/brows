using System;
using System.Linq;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryColumnLayoutTest {
    [TestCase(800, 500, 80, 160, 60)]
    [TestCase(420, 120, 80, 160, 60)]
    [TestCase(400, 120, 80, 140, 60)]
    [TestCase(200, 120, 80, 0, 0)]
    [TestCase(70, 70, 0, 0, 0)]
    [TestCase(0, 0, 0, 0, 0)]
    public void Allocate_PreservesCompactMetadata(double budget, double name, double size, double date, double ext) {
        EntryColumnLayout.Column[] columns = [new(400, 60), new(80, 50), new(160, 80), new(60, 60)];
        Assert.That(EntryColumnLayout.Allocate(columns, budget), Is.EqualTo(new[] { name, size, date, ext }));
    }

    [TestCase(600, 320, 200)]
    [TestCase(800, 520, 200)]
    [TestCase(804, 523, 201)]
    public void Allocate_CapsOutliers(double budget, double primary, double secondary) {
        EntryColumnLayout.Column[] columns = [new(400, 60), new(5000, 60), new(80, 50)];
        Assert.That(EntryColumnLayout.Allocate(columns, budget), Is.EqualTo(new[] { primary, secondary, 80 }));
    }

    [Test]
    public void Allocate_ExplicitRequestsCanOverflow() {
        EntryColumnLayout.Column[] columns = [new(400, 60), new(600, 60, 600)];
        Assert.That(EntryColumnLayout.Allocate(columns, 400), Is.EqualTo(new[] { 120, 600 }));
        Assert.That(EntryColumnLayout.Allocate([], 400), Is.Empty);
    }

    [TestCase(399, 120, 399)]
    [TestCase(400, 120, 400)]
    public void Allocate_ExplicitRequestsPreservePrimaryMinimumAtBudgetBoundary(
        double requested,
        double primary,
        double explicitWidth) {
        EntryColumnLayout.Column[] columns = [new(400, 60), new(requested, 60, requested)];

        Assert.That(EntryColumnLayout.Allocate(columns, 400), Is.EqualTo(new[] { primary, explicitWidth }));
    }

    [TestCase(1.25)]
    [TestCase(1.5)]
    [TestCase(2)]
    public void Allocate_RoundsWithoutOverflow(double scale) {
        EntryColumnLayout.Column[] columns = [new(400, 60), new(85.7, 50), new(160.3, 80)];
        var widths = EntryColumnLayout.Allocate(columns, 401.3, scale);
        Assert.That(widths.Sum(), Is.InRange(401.3 - 1 / scale, 401.3));
        Assert.That(widths.All(width => Math.Abs(width * scale - Math.Round(width * scale)) < 0.00001), Is.True);
    }

    [Test]
    public void Allocate_TiesKeepColumnOrderAndInvalidDemandsAreSafe() {
        EntryColumnLayout.Column[] columns = [new(400, 60), new(100, 50), new(100, 50)];
        Assert.That(EntryColumnLayout.Allocate(columns, 250), Is.EqualTo(new[] { 120, 80, 50 }));
        Assert.That(EntryColumnLayout.Allocate([new(double.NaN, double.NaN)], 200), Is.EqualTo(new[] { 200 }));
        Assert.That(EntryColumnLayout.Allocate(columns, double.NaN), Is.All.Zero);
    }
}
