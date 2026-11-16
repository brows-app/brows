using System;
using System.Globalization;

namespace Brows.Data;

[TestFixture]
internal sealed class MBoxDateConverterTest {
    private static readonly DateTimeOffset Date =
        new DateTimeOffset(2015, 9, 17, 17, 54, 37, TimeSpan.Zero);

    [Test]
    public void Convert_WritesTheDateOfTheMessageInUtc() {
        var converter = new MBoxDateConverter();
        var text = converter.Convert(Date, null, CultureInfo.InvariantCulture);
        Assert.That(text, Is.EqualTo("2015-09-17 17:54:37 UTC"));
    }

    [Test]
    public void Convert_MovesTheDateOfTheMessageToUtc() {
        var converter = new MBoxDateConverter();
        var date = new DateTimeOffset(2015, 9, 17, 19, 54, 37, TimeSpan.FromHours(2));
        var text = converter.Convert(date, null, CultureInfo.InvariantCulture);
        Assert.That(text, Is.EqualTo("2015-09-17 17:54:37 UTC"));
    }

    [Test]
    public void Convert_WritesNothingForAMessageThatHasNoDate() {
        var converter = new MBoxDateConverter();
        var text = converter.Convert(null, null, CultureInfo.InvariantCulture);
        Assert.That(text, Is.Null);
    }

    [Test]
    public void Convert_WritesNothingForAValueThatIsNotADate() {
        var converter = new MBoxDateConverter();
        var text = converter.Convert("not a date", null, CultureInfo.InvariantCulture);
        Assert.That(text, Is.Null);
    }

    [Test]
    public void Convert_UsesTheFormatOfTheParameter() {
        var converter = new MBoxDateConverter();
        var text = converter.Convert(Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.That(text, Is.EqualTo("2015-09-17"));
    }
}
