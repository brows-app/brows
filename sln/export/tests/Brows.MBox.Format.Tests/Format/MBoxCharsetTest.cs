using System;
using System.Linq;
using System.Text;
using Brows.MBox.Format;

namespace Brows.MBox;

/// <summary>
/// Tests the decoding of the text of a message with the character set it declares.
/// </summary>
[TestFixture]
public sealed class MBoxCharsetTest {
    [Test]
    public void Body_ReadsUtf8Text() {
        var headers = new[] {
            "Content-Type: text/plain; charset=utf-8",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("Café\n", headers));
        Assert.That(message.Body.Text, Is.EqualTo("Café\n"));
    }

    [Test]
    public void Body_ReadsLatin1Text() {
        var headers = new[] {
            "Content-Type: text/plain; charset=iso-8859-1",
        };
        var body = Encoding.Latin1.GetBytes("Café\n");
        var message = MBoxFixture.MessageOfBytes(body, headers);
        Assert.That(message.Body.Text, Is.EqualTo("Café\n"));
    }

    [Test]
    public void Body_ReadsWindows1252Punctuation() {
        var headers = new[] {
            "Content-Type: text/plain; charset=windows-1252",
        };
        /*
         * The curly double quotes are not valid Latin-1 characters, so a Latin-1 read would
         * show them as control characters.
         */
        var body = CodePagesEncodingProvider.Instance.GetEncoding("windows-1252").GetBytes("“Quoted”\n");
        var message = MBoxFixture.MessageOfBytes(body, headers);
        Assert.That(message.Body.Text, Is.EqualTo("“Quoted”\n"));
    }

    [Test]
    public void Body_ReadsANonWesternCharset() {
        var headers = new[] {
            "Content-Type: text/plain; charset=shift_jis",
        };
        var body = CodePagesEncodingProvider.Instance.GetEncoding("shift_jis").GetBytes("日本語\n");
        var message = MBoxFixture.MessageOfBytes(body, headers);
        Assert.That(message.Body.Text, Is.EqualTo("日本語\n"));
    }

    [Test]
    public void Body_AcceptsACharacterSetAlias() {
        var headers = new[] {
            "Content-Type: text/plain; charset=UTF8",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("Text\n", headers));
        Assert.That(message.Body.Text, Is.EqualTo("Text\n"));
    }

    [Test]
    public void Body_ReportsAnUnknownCharacterSet() {
        var headers = new[] {
            "Content-Type: text/plain; charset=not-a-charset",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("Text\n", headers));
        Assert.That(
            message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.UnsupportedCharset),
            Is.True);
    }

    [Test]
    public void Body_ReadsUtf8TextWhenTheCharacterSetIsUnknown() {
        var headers = new[] {
            "Content-Type: text/plain; charset=not-a-charset",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("Text\n", headers));
        Assert.That(message.Body.Text, Is.EqualTo("Text\n"));
    }

    [Test]
    public void Body_ReportsAnUnknownCharacterSetEvenWhenTheTextIsRead() {
        var headers = new[] {
            "Content-Type: text/plain; charset=not-a-charset",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("Text\n", headers));
        Assert.That(
            message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.UnsupportedCharset),
            Is.True);
        Assert.That(message.Body.Text, Is.EqualTo("Text\n"));
    }

    [Test]
    public void Body_ReportsTextThatIsNotValidInTheDeclaredCharacterSet() {
        var headers = new[] {
            "Content-Type: text/plain; charset=utf-8",
        };
        /*
         * The lone continuation byte is not valid UTF-8.
         */
        var body = new byte[] { 0x54, 0x65, 0x78, 0x74, 0x80, 0x0A };
        var message = MBoxFixture.MessageOfBytes(body, headers);
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Malformed));
        Assert.That(
            message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedEncoding),
            Is.True);
    }

    [Test]
    public void Body_ReportsTextThatIsNotValidInAscii() {
        var headers = new[] {
            "Content-Type: text/plain; charset=us-ascii",
        };
        var body = new byte[] { 0xE9, 0x0A };

        var message = MBoxFixture.MessageOfBytes(body, headers);

        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Malformed));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedEncoding), Is.True);
    }

    [Test]
    public void Body_ReportsAnIncompleteCodePageSequence() {
        var headers = new[] {
            "Content-Type: text/plain; charset=shift_jis",
        };
        var body = new byte[] { 0x82, 0x0A };

        var message = MBoxFixture.MessageOfBytes(body, headers);

        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Malformed));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedEncoding), Is.True);
    }

    [Test]
    public void Body_ReadsTextAsUtf8WhenNoCharacterSetIsDeclared() {
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("Café\n"));
        Assert.That(message.Body.Text, Is.EqualTo("Café\n"));
    }

    [Test]
    public void Body_ReadsTextAsLatin1WhenItIsNotValidUtf8() {
        /*
         * The lone continuation byte is not valid UTF-8, so the text is read as Latin-1.
         */
        var body = new byte[] { 0x54, 0x65, 0x78, 0x74, 0x80, 0x0A };
        var message = MBoxFixture.MessageOfBytes(body);
        Assert.That(message.Body.Text, Is.EqualTo("Text\u0080\n"));
    }
}
