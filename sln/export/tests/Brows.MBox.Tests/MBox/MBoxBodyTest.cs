using System;
using System.Linq;
using Brows.MBox.Format;

namespace Brows.MBox;

/// <summary>
/// Tests the reading of a body and the selection of the text to display.
/// </summary>
[TestFixture]
internal sealed class MBoxBodyTest {
    [Test]
    public void Body_ReadsAPlainTextBody() {
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("The body.\n"));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Complete));
        Assert.That(message.Body.Text, Is.EqualTo("The body.\n"));
    }

    [Test]
    public void Body_PreservesTheFinalLineWithoutInventingATerminator() {
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("The body."));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Complete));
        Assert.That(message.Body.Text, Is.EqualTo("The body."));
    }

    [Test]
    public void Body_UsesTextPlainWhenNoContentTypeIsDeclared() {
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("The body.\n"));
        Assert.That(message.Body.ContentType.Full, Is.EqualTo("text/plain"));
    }

    [Test]
    public void Body_DecodesBase64() {
        var headers = new[] {
            "Content-Type: text/plain; charset=utf-8",
            "Content-Transfer-Encoding: base64",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("VGhlIGJvZHk=\n", headers));
        Assert.That(message.Body.Text, Is.EqualTo("The body"));
    }

    [Test]
    public void Body_DecodesQuotedPrintable() {
        var headers = new[] {
            "Content-Type: text/plain; charset=utf-8",
            "Content-Transfer-Encoding: quoted-printable",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("The=20body=\n", headers));
        Assert.That(message.Body.Text, Is.EqualTo("The body"));
    }

    [Test]
    public void Body_ReportsMalformedBase64() {
        var headers = new[] {
            "Content-Type: text/plain; charset=utf-8",
            "Content-Transfer-Encoding: base64",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("not base64!!\n", headers));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedEncoding), Is.True);
    }

    [Test]
    public void Body_SelectsTheLastInlineTextOfAnAlternative() {
        var headers = new[] {
            "Content-Type: multipart/alternative; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "First alternative.\n" +
            "--b\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "Second alternative.\n" +
            "--b--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Text, Is.EqualTo("Second alternative.\n"));
    }

    [Test]
    public void Body_ReportsTheHtmlOfAnAlternativeSeparately() {
        var headers = new[] {
            "Content-Type: multipart/alternative; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "The plain alternative.\n" +
            "--b\n" +
            "Content-Type: text/html\n" +
            "\n" +
            "<p>The html alternative.</p>\n" +
            "--b--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Text, Is.EqualTo("The plain alternative.\n"));
        Assert.That(message.Body.Html, Is.EqualTo("<p>The html alternative.</p>\n"));
    }

    [Test]
    public void Body_SelectsTheHtmlWhenThereIsNoPlainTextAlternative() {
        var headers = new[] {
            "Content-Type: multipart/alternative; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/html\n" +
            "\n" +
            "<p>The html alternative.</p>\n" +
            "--b--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Text, Is.Null);
        Assert.That(message.Body.Html, Is.EqualTo("<p>The html alternative.</p>\n"));
    }

    [Test]
    public void Body_DoesNotUseATextAttachmentAsTheBody() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/plain; name=\"notes.txt\"\n" +
            "Content-Disposition: attachment; filename=\"notes.txt\"\n" +
            "\n" +
            "The attachment.\n" +
            "--b\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "The body.\n" +
            "--b--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Text, Is.EqualTo("The body.\n"));
    }

    [Test]
    public void Body_KeepsAnAttachmentOutOfTheDisplayBody() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "The body.\n" +
            "--b\n" +
            "Content-Type: application/octet-stream\n" +
            "Content-Disposition: attachment; filename=\"data.bin\"\n" +
            "\n" +
            "Binary data.\n" +
            "--b--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Text, Is.EqualTo("The body.\n"));
        var attachment = message.Body.Parts.First(item => item.IsAttachmentPart);
        Assert.That(attachment.FileName, Is.EqualTo("data.bin"));
        Assert.That(attachment.Text, Is.Null);
    }

    [Test]
    public void Body_SelectsTheFirstChildOfARelatedPart() {
        var headers = new[] {
            "Content-Type: multipart/related; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/html\n" +
            "\n" +
            "<p>The root.</p>\n" +
            "--b\n" +
            "Content-Type: image/png\n" +
            "Content-ID: <image>\n" +
            "\n" +
            "The image.\n" +
            "--b--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Html, Is.EqualTo("<p>The root.</p>\n"));
    }

    [Test]
    public void Body_SelectsTheStartOfARelatedPart() {
        var headers = new[] {
            "Content-Type: multipart/related; boundary=\"b\"; start=\"<root>\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/html\n" +
            "Content-ID: <other>\n" +
            "\n" +
            "<p>The other.</p>\n" +
            "--b\n" +
            "Content-Type: text/html\n" +
            "Content-ID: <root>\n" +
            "\n" +
            "<p>The root.</p>\n" +
            "--b--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Html, Is.EqualTo("<p>The root.</p>\n"));
    }

    [Test]
    public void Body_ReportsAMissingRootOfARelatedPart() {
        var headers = new[] {
            "Content-Type: multipart/related; boundary=\"b\"; start=\"<missing>\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/html\n" +
            "\n" +
            "<p>The root.</p>\n" +
            "--b--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Html, Is.Null);
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Unavailable));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedMime), Is.True);
    }

    [Test]
    public void Body_ReportsAnEncryptedBodyAsUnavailable() {
        var headers = new[] {
            "Content-Type: multipart/encrypted; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: application/octet-stream\n" +
            "\n" +
            "The encrypted data.\n" +
            "--b--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Unavailable));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.UnsupportedBody), Is.True);
    }

    [Test]
    public void Body_ReportsAMissingClosingBoundary() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "The body.\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedMime), Is.True);
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Malformed));
    }

    [Test]
    public void Body_IgnoresThePreambleAndTheEpilogue() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "This is the preamble.\n" +
            "--b\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "The body.\n" +
            "--b--\n" +
            "This is the epilogue.\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Text, Is.EqualTo("The body.\n"));
    }

    [Test]
    public void Body_TruncatesTheTextThatIsOverTheCharacterLimit() {
        var options = MBoxParserOptions.Default with { MaxPreviewCharacters = 5 };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("The body is long.\n"), 0, options);
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Truncated));
        Assert.That(message.Body.Text, Is.EqualTo("The b"));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.LimitReached), Is.True);
    }

    [Test]
    public void Body_KeepsTheWholeLineWhenItIsLongerThanTheHeaderLimit() {
        var bodyText = new string('A', 80) + "TAIL\n";
        var options = MBoxParserOptions.Default with { MaxHeaderBytes = 32 };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(bodyText), 0, options);
        Assert.That(message.Body.Text, Is.EqualTo(bodyText));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Complete));
    }

    [Test]
    public void Body_AppliesTheDecodedByteBudgetAcrossAllMimeParts() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "--b\nContent-Type: text/plain\n\n123456789\n" +
            "--b\nContent-Type: text/plain\n\nabcdefgh\n" +
            "--b\nContent-Type: text/plain\n\nABCDEFGHIJ\n" +
            "--b--\n";
        var options = MBoxParserOptions.Default with { MaxDecodedTextBytes = 20 };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers), 0, options);
        var retainedBytes = message.Body.Parts
            .Where(part => part.Text is not null)
            .Sum(part => (long)part.Text.Length * sizeof(char));
        Assert.That(retainedBytes, Is.LessThanOrEqualTo(20));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Truncated));
    }

    [Test]
    public void Body_AppliesTheCharacterBudgetAcrossAllMimeParts() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "--b\nContent-Type: text/plain\n\n1234\n" +
            "--b\nContent-Type: text/plain\n\n5678\n" +
            "--b--\n";
        var options = MBoxParserOptions.Default with { MaxPreviewCharacters = 5 };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers), 0, options);
        var retainedCharacters = message.Body.Parts
            .Where(part => part.Text is not null)
            .Sum(part => part.Text.Length);
        Assert.That(retainedCharacters, Is.LessThanOrEqualTo(5));
    }

    [Test]
    public void Body_TruncatesTheTextThatIsOverTheInputLimit() {
        var options = MBoxParserOptions.Default with { MaxPreviewInputBytes = 4 };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("The body is long.\n"), 0, options);
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Truncated));
    }

    [Test]
    public void Body_ReportsTheLengthOfTheBody() {
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("The body.\n"));
        Assert.That(message.Body.Length, Is.EqualTo("The body.\n".Length));
    }
}
