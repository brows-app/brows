using System;
using System.Linq;
using Brows.MBox.Format;

namespace Brows.MBox;

/// <summary>
/// Tests the parsing of the dates of envelopes and header fields.
/// </summary>
[TestFixture]
public sealed class MBoxDateTest {
    [Test]
    public void EnvelopeDate_UsesTheOffsetOfTheGoogleShape() {
        var records = MBoxFixture.Records(MBoxFixture.Mailbox(
            "Body.\n",
            envelope: "From sender@example.com Thu Sep 17 18:09:44 +0200 2015"));
        var date = records[0].Envelope.Date;
        Assert.That(date.HasValue, Is.True);
        Assert.That(date.Value, Is.EqualTo(new DateTimeOffset(2015, 9, 17, 18, 9, 44, TimeSpan.FromHours(2))));
    }

    [Test]
    public void EnvelopeDate_UsesUtcForTheClassicShape() {
        var records = MBoxFixture.Records(MBoxFixture.Mailbox(
            "Body.\n",
            envelope: "From sender@example.com Thu Sep 17 18:09:44 2015"));
        var date = records[0].Envelope.Date;
        Assert.That(date.HasValue, Is.True);
        Assert.That(date.Value.Offset, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void EnvelopeDate_AcceptsASpacePaddedDay() {
        var records = MBoxFixture.Records(MBoxFixture.Mailbox(
            "Body.\n",
            envelope: "From sender@example.com Thu Sep  7 18:09:44 +0000 2015"));
        var date = records[0].Envelope.Date;
        Assert.That(date.HasValue, Is.True);
        Assert.That(date.Value.Day, Is.EqualTo(7));
    }

    [Test]
    public void EnvelopeDate_AcceptsANumericOffsetWithAMinusSign() {
        var records = MBoxFixture.Records(MBoxFixture.Mailbox(
            "Body.\n",
            envelope: "From sender@example.com Thu Sep 17 18:09:44 -0500 2015"));
        var date = records[0].Envelope.Date;
        Assert.That(date.HasValue, Is.True);
        Assert.That(date.Value.Offset, Is.EqualTo(TimeSpan.FromHours(-5)));
    }

    [Test]
    public void EnvelopeDate_IsNullForADateThatIsOutOfRange() {
        var (records, diagnostics) = MBoxFixture.Scan(MBoxFixture.Mailbox(
            "Body.\n",
            envelope: "From sender@example.com Thu Feb 30 18:09:44 +0000 2015"));
        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0].Envelope.Date.HasValue, Is.False);
        Assert.That(diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedDate), Is.True);
    }

    [Test]
    public void EnvelopeDate_KeepsTheDateWhenTheWeekdayDisagrees() {
        var (records, diagnostics) = MBoxFixture.Scan(MBoxFixture.Mailbox(
            "Body.\n",
            envelope: "From sender@example.com Wed Sep 17 18:09:44 +0000 2015"));
        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0].Envelope.Date.HasValue, Is.True);
        Assert.That(diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedDate), Is.True);
    }

    [Test]
    public void EnvelopeDate_IsNullForAnImpossibleTime() {
        var (records, diagnostics) = MBoxFixture.Scan(MBoxFixture.Mailbox(
            "Body.\n",
            envelope: "From sender@example.com Thu Sep 17 25:09:44 +0000 2015"));
        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0].Envelope.Date.HasValue, Is.False);
        Assert.That(diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedDate), Is.True);
    }

    [Test]
    public void EnvelopeDate_IsNullForAnImpossibleOffset() {
        var (records, diagnostics) = MBoxFixture.Scan(MBoxFixture.Mailbox(
            "Body.\n",
            envelope: "From sender@example.com Thu Sep 17 18:09:44 +9900 2015"));
        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0].Envelope.Date.HasValue, Is.False);
        Assert.That(diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedDate), Is.True);
    }

    [Test]
    public void EnvelopeDate_IsNotRecognizedForAnUnknownShape() {
        var (records, _) = MBoxFixture.Scan(MBoxFixture.Mailbox(
            "Body.\n",
            envelope: "From sender@example.com the seventeenth of September"));
        Assert.That(records, Is.Empty);
    }

    [Test]
    public void HeaderDate_IsParsedWithItsNumericOffset() {
        var headers = new[] {
            "Date: Thu, 17 Sep 2015 17:54:37 +0000",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        var date = MBoxDate.TryParseHeader(message.Headers["Date"].RawValue, out var parsed);
        Assert.That(date, Is.True);
        Assert.That(parsed, Is.EqualTo(new DateTimeOffset(2015, 9, 17, 17, 54, 37, TimeSpan.Zero)));
    }

    [Test]
    public void HeaderDate_IsParsedWithAnObsoleteNamedZone() {
        var headers = new[] {
            "Date: Thu, 17 Sep 2015 17:54:37 EST",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        var date = MBoxDate.TryParseHeader(message.Headers["Date"].RawValue, out var parsed);
        Assert.That(date, Is.True);
        Assert.That(parsed.Value.Offset, Is.EqualTo(TimeSpan.FromHours(-5)));
    }

    [Test]
    public void HeaderDate_IsParsedWithAComment() {
        var headers = new[] {
            "Date: Thu, 17 Sep 2015 17:54:37 +0000 (UTC)",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        var date = MBoxDate.TryParseHeader(message.Headers["Date"].RawValue, out var parsed);
        Assert.That(date, Is.True);
        Assert.That(parsed, Is.EqualTo(new DateTimeOffset(2015, 9, 17, 17, 54, 37, TimeSpan.Zero)));
    }

    [Test]
    public void HeaderDate_IsNullForAnUnknownZone() {
        var headers = new[] {
            "Date: Thu, 17 Sep 2015 17:54:37 XYZ",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        var date = MBoxDate.TryParseHeader(message.Headers["Date"].RawValue, out var parsed);
        Assert.That(date, Is.False);
        Assert.That(parsed.HasValue, Is.False);
    }

    [Test]
    public void HeaderDate_IsNullForAMissingZone() {
        var headers = new[] {
            "Date: Thu, 17 Sep 2015 17:54:37",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        var date = MBoxDate.TryParseHeader(message.Headers["Date"].RawValue, out var parsed);
        Assert.That(date, Is.False);
        Assert.That(parsed.HasValue, Is.False);
    }

    [Test]
    public void HeaderDate_IsNullForAnInvalidDate() {
        var headers = new[] {
            "Date: Thu, 32 Sep 2015 17:54:37 +0000",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        var date = MBoxDate.TryParseHeader(message.Headers["Date"].RawValue, out var parsed);
        Assert.That(date, Is.False);
        Assert.That(parsed.HasValue, Is.False);
    }

    [TestCase("Thu, 17 Sep 2015 18:09:44 +1400", true)]
    [TestCase("Thu, 17 Sep 2015 18:09:44 +1401", false)]
    [TestCase("Thu, 17 Sep 2015 18:09:44 -1459", false)]
    [TestCase("1 Jan 0001 00:00:00 +0100", false)]
    [TestCase("31 Dec 9999 23:59:59 -0100", false)]
    public void HeaderDate_RejectsUnrepresentableOffsetsAndUtcInstants(string value, bool expected) {
        var parsed = MBoxDate.TryParseHeader(value, out var date);

        Assert.That(parsed, Is.EqualTo(expected));
        Assert.That(date.HasValue, Is.EqualTo(expected));
    }

    [TestCase("00", 2000)]
    [TestCase("49", 2049)]
    [TestCase("50", 1950)]
    [TestCase("75", 1975)]
    [TestCase("99", 1999)]
    [TestCase("100", 2000)]
    [TestCase("999", 2899)]
    public void HeaderDate_UsesTheFixedRfcYearAdjustments(string year, int expectedYear) {
        var value = $"1 Jan {year} 00:00 +0000";
        var parsed = MBoxDate.TryParseHeader(value, out var date);

        Assert.That(parsed, Is.True);
        Assert.That(date.Value.Year, Is.EqualTo(expectedYear));
    }

    [Test]
    public void HeaderDate_AllowsMissingSeconds() {
        var parsed = MBoxDate.TryParseHeader("17 Sep 2015 18:09 +0000", out var date);

        Assert.That(parsed, Is.True);
        Assert.That(date, Is.EqualTo(new DateTimeOffset(2015, 9, 17, 18, 9, 0, TimeSpan.Zero)));
    }

    [Test]
    public void HeaderDate_RejectsALeapSecondAndReportsIt() {
        var headers = new[] {
            "Date: 31 Dec 2016 23:59:60 +0000",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));

        Assert.That(message.Headers.Date.HasValue, Is.False);
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedDate), Is.True);
    }

    [TestCase("+1400", true)]
    [TestCase("+1401", false)]
    [TestCase("-1459", false)]
    public void EnvelopeDate_RejectsUnrepresentableOffsetsWithoutThrowing(string offset, bool expected) {
        var envelope = $"From sender@example.com Thu Sep 17 18:09:44 {offset} 2015";

        var (records, diagnostics) = MBoxFixture.Scan(MBoxFixture.Mailbox("Body.\n", envelope: envelope));

        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0].Envelope.Date.HasValue, Is.EqualTo(expected));
        if (expected == false) {
            Assert.That(diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedDate), Is.True);
        }
    }

    [TestCase("Thu Jan 1 00:00:00 +0100 0001")]
    [TestCase("Fri Dec 31 23:59:59 -0100 9999")]
    public void EnvelopeDate_RejectsUtcOverflowWithoutThrowing(string date) {
        var envelope = "From sender@example.com " + date;

        var (records, diagnostics) = MBoxFixture.Scan(MBoxFixture.Mailbox("Body.\n", envelope: envelope));

        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0].Envelope.Date.HasValue, Is.False);
        Assert.That(diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedDate), Is.True);
    }

    [Test]
    public void Scan_MalformedEnvelopeDate_DoesNotPreventAdjacentRecords() {
        var first = MBoxFixture.Mailbox("First body.\n");
        var invalid = MBoxFixture.Mailbox(
            "Middle body.\n",
            envelope: "From sender@example.com Thu Sep 17 18:09:44 +1401 2015");
        var last = MBoxFixture.Mailbox("Last body.\n");
        var text = first + "\n" + invalid + "\n" + last;

        var (records, diagnostics) = MBoxFixture.Scan(text);

        Assert.That(records, Has.Count.EqualTo(3));
        Assert.That(records[1].Envelope.Date.HasValue, Is.False);
        Assert.That(diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedDate), Is.True);
    }

    [Test]
    public void HeaderDate_DoesNotInvalidateTheMessage() {
        var headers = new[] {
            "Date: not a date at all",
            "Subject: A subject",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers["Subject"].Value, Is.EqualTo("A subject"));
        Assert.That(message.Body.Text, Is.EqualTo("Body.\n"));
    }
}
