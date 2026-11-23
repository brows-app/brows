using System;
using System.Linq;
using Brows.MBox.Format;

namespace Brows.MBox;

/// <summary>
/// Tests the unquoting of the body lines of a mailbox, in both supported dialects.
/// </summary>
[TestFixture]
internal sealed class MBoxDialectTest {
    [Test]
    public void Unquote_RemovesOneQuoteMarkInTheMboxrdDialect() {
        Assert.That(MBoxUnquote.Unquote(">From the beginning", MBoxFormat.Mboxrd), Is.EqualTo("From the beginning"));
    }

    [Test]
    public void Unquote_RemovesOnlyOneQuoteMarkInTheMboxrdDialect() {
        Assert.That(MBoxUnquote.Unquote(">>From the beginning", MBoxFormat.Mboxrd), Is.EqualTo(">From the beginning"));
    }

    [Test]
    public void Unquote_RemovesOneQuoteMarkInTheMboxoDialect() {
        Assert.That(MBoxUnquote.Unquote(">From the beginning", MBoxFormat.Mboxo), Is.EqualTo("From the beginning"));
    }

    [Test]
    public void Unquote_LeavesADoubleQuoteMarkInTheMboxoDialect() {
        Assert.That(MBoxUnquote.Unquote(">>From the beginning", MBoxFormat.Mboxo), Is.EqualTo(">>From the beginning"));
    }

    [Test]
    public void Unquote_LeavesALineThatDoesNotStartWithFrom() {
        Assert.That(MBoxUnquote.Unquote("> The beginning", MBoxFormat.Mboxrd), Is.EqualTo("> The beginning"));
        Assert.That(MBoxUnquote.Unquote("> The beginning", MBoxFormat.Mboxo), Is.EqualTo("> The beginning"));
    }

    [Test]
    public void Unquote_LeavesALineThatIsNotQuoted() {
        Assert.That(MBoxUnquote.Unquote("From the beginning", MBoxFormat.Mboxrd), Is.EqualTo("From the beginning"));
    }

    [Test]
    public void Body_UsesTheMboxrdUnquotingByDefault() {
        var body = ">From the beginning\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body));
        Assert.That(message.Body.Text, Is.EqualTo("From the beginning\n"));
    }

    [Test]
    public void Body_UsesTheMboxoUnquotingWhenItIsSelected() {
        var body = ">>From the beginning\n";
        var options = MBoxParserOptions.Default.WithFormat(MBoxFormat.Mboxo);
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body), 0, options);
        Assert.That(message.Body.Text, Is.EqualTo(">>From the beginning\n"));
    }

    [Test]
    public void Body_UsesTheMboxrdUnquotingWhenItIsSelected() {
        var body = ">>From the beginning\n";
        var options = MBoxParserOptions.Default.WithFormat(MBoxFormat.Mboxrd);
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body), 0, options);
        Assert.That(message.Body.Text, Is.EqualTo(">From the beginning\n"));
    }

    [Test]
    public void Record_ReportsTheDialectItWasReadWith() {
        var options = MBoxParserOptions.Default.WithFormat(MBoxFormat.Mboxo);
        var records = MBoxFixture.Records(MBoxFixture.Mailbox("Body.\n"), options);
        Assert.That(records[0].Format, Is.EqualTo(MBoxFormat.Mboxo));
    }

    [Test]
    public void Options_DefaultToTheMboxrdDialect() {
        Assert.That(MBoxParserOptions.Default.Format, Is.EqualTo(MBoxFormat.Mboxrd));
    }

    [Test]
    public void Options_RejectAnUndefinedDialect() {
        Assert.That(
            () => MBoxParserOptions.Default with { Format = (MBoxFormat)99 },
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}
