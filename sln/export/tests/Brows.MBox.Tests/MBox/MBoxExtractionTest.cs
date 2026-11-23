using System;
using System.Linq;
using System.Text;
using Brows.MBox.Format;

namespace Brows.MBox;

/// <summary>
/// Tests the extraction of a stored message as an Internet message.
/// </summary>
[TestFixture]
internal sealed class MBoxExtractionTest {
    [Test]
    public void Extract_DoesNotIncludeTheEnvelope() {
        var headers = new[] {
            "Subject: A subject",
        };
        var text = MBoxFixture.Mailbox("The body.\n", headers);
        var extracted = MBoxFixture.Extract(text);
        Assert.That(extracted, Does.Not.Contain(MBoxFixture.Envelope));
        Assert.That(extracted, Does.StartWith("Subject: A subject\n"));
    }

    [Test]
    public void Extract_KeepsTheHeaderFieldsAndTheBody() {
        var headers = new[] {
            "Subject: A subject",
            "From: Someone <someone@example.com>",
        };
        var text = MBoxFixture.Mailbox("The body.\n", headers);
        var extracted = MBoxFixture.Extract(text);
        Assert.That(extracted, Is.EqualTo("Subject: A subject\nFrom: Someone <someone@example.com>\n\nThe body.\n"));
    }

    [Test]
    public void Extract_RemovesOneQuoteMarkInTheMboxrdDialect() {
        var text = MBoxFixture.Mailbox(">From the beginning\n");
        var extracted = MBoxFixture.Extract(text);
        Assert.That(extracted, Is.EqualTo("\nFrom the beginning\n"));
    }

    [Test]
    public void Extract_RemovesOnlyOneQuoteMarkInTheMboxrdDialect() {
        var text = MBoxFixture.Mailbox(">>From the beginning\n");
        var extracted = MBoxFixture.Extract(text);
        Assert.That(extracted, Is.EqualTo("\n>From the beginning\n"));
    }

    [Test]
    public void Extract_LeavesADoubleQuoteMarkInTheMboxoDialect() {
        var text = MBoxFixture.Mailbox(">>From the beginning\n");
        var options = MBoxParserOptions.Default.WithFormat(MBoxFormat.Mboxo);
        var extracted = MBoxFixture.Extract(text, 0, options);
        Assert.That(extracted, Is.EqualTo("\n>>From the beginning\n"));
    }

    [Test]
    public void Extract_KeepsTheLineTerminatorsOfTheSource() {
        var headers = new[] {
            "Subject: A subject",
        };
        var text = MBoxFixture.Mailbox("The body.\r\n", headers, terminator: "\r\n");
        var extracted = MBoxFixture.Extract(text);
        Assert.That(extracted, Is.EqualTo("Subject: A subject\r\n\r\nThe body.\r\n"));
    }

    [Test]
    public void Extract_KeepsTheMIMEBoundariesAndTheAttachmentBytes() {
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
            "Content-Transfer-Encoding: base64\n" +
            "\n" +
            "VGhlIGJ5dGVz\n" +
            "--b--\n";
        var text = MBoxFixture.Mailbox(body, headers);
        var extracted = MBoxFixture.Extract(text);
        Assert.That(extracted, Does.Contain("--b\n"));
        Assert.That(extracted, Does.Contain("VGhlIGJ5dGVz\n"));
        Assert.That(extracted, Does.Contain("--b--\n"));
    }

    [Test]
    public void Extract_KeepsTheBytesOfASignedMessage() {
        var headers = new[] {
            "Content-Type: multipart/signed; boundary=\"b\"; protocol=\"application/pkcs7-signature\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "The body.\n" +
            "--b\n" +
            "Content-Type: application/pkcs7-signature\n" +
            "\n" +
            "AAAA\n" +
            "--b--\n";
        var text = MBoxFixture.Mailbox(body, headers);
        var extracted = MBoxFixture.Extract(text);
        Assert.That(extracted, Does.Contain("AAAA\n"));
        Assert.That(extracted, Does.Contain("The body.\n"));
    }

    [Test]
    public void Extract_KeepsTheWholeBodyEvenWhenThePreviewIsTruncated() {
        var options = MBoxParserOptions.Default with { MaxPreviewCharacters = 5 };
        var text = MBoxFixture.Mailbox("The body is long.\n");
        var extracted = MBoxFixture.Extract(text, 0, options);
        Assert.That(extracted, Is.EqualTo("\nThe body is long.\n"));
    }

    [Test]
    public void Extract_ExtractsOnlyTheSelectedRecord() {
        var text = MBoxFixture.Mailbox(["First body.\n", "Second body.\n"]);
        var extracted = MBoxFixture.Extract(text, 1);
        Assert.That(extracted, Is.EqualTo("\nSecond body.\n"));
    }

    [Test]
    public void Extract_KeepsTheBodyOfARecordWithoutHeaders() {
        var text = MBoxFixture.Mailbox("The body.\n");
        var extracted = MBoxFixture.Extract(text);
        Assert.That(extracted, Is.EqualTo("\nThe body.\n"));
    }

    [TestCase("\n", MBoxFormat.Mboxrd)]
    [TestCase("\r\n", MBoxFormat.Mboxrd)]
    [TestCase("\n", MBoxFormat.Mboxo)]
    [TestCase("\r\n", MBoxFormat.Mboxo)]
    public void Extract_KeepsOversizedHeadersAndUnquotesTheBody(string terminator, MBoxFormat format) {
        var headers = new[] {
            $"Subject: {new string('x', 80)}",
            "From: sender@example.com",
            " X-Folded: value",
        };
        var body = ">From body marker" + terminator;
        var text = MBoxFixture.Mailbox(body, headers, terminator: terminator);
        var options = MBoxParserOptions.Default with { Format = format, MaxHeaderBytes = 32 };

        var extracted = MBoxFixture.Extract(text, 0, options);

        var expected = string.Join(terminator, headers) +
                       terminator +
                       terminator +
                       "From body marker" +
                       terminator;
        Assert.That(extracted, Is.EqualTo(expected));
    }

    [TestCase("\n", MBoxFormat.Mboxrd)]
    [TestCase("\r\n", MBoxFormat.Mboxrd)]
    [TestCase("\n", MBoxFormat.Mboxo)]
    [TestCase("\r\n", MBoxFormat.Mboxo)]
    public void Extract_KeepsAnUnterminatedOversizedFoldedHeaderExactly(string terminator,
                                                                         MBoxFormat format) {
        var headers = new[] {
            "Subject: A",
            " 0123456789",
            " 0123456789",
            " 0123456789",
            "X-Internal: secret",
        };
        var message = string.Join(terminator, headers) + terminator +
                      ">From body marker" + terminator;
        var text = MBoxFixture.Envelope + terminator + message;
        var options = MBoxParserOptions.Default with { Format = format, MaxHeaderBytes = 32 };

        var extracted = MBoxFixture.Extract(text, 0, options);

        Assert.That(extracted, Is.EqualTo(message));
    }

    [TestCase("\n", MBoxFormat.Mboxrd)]
    [TestCase("\r\n", MBoxFormat.Mboxrd)]
    [TestCase("\n", MBoxFormat.Mboxo)]
    [TestCase("\r\n", MBoxFormat.Mboxo)]
    public void Extract_KeepsAnOversizedFoldedHeaderAndUnquotesTheBody(string terminator,
                                                                       MBoxFormat format) {
        var headers = new[] {
            "Subject: A",
            " 0123456789",
            " 0123456789",
            " 0123456789",
            "From: sender@example.com",
        };
        var body = ">From body marker" + terminator;
        var text = MBoxFixture.Mailbox(body, headers, terminator: terminator);
        var options = MBoxParserOptions.Default with { Format = format, MaxHeaderBytes = 32 };

        var extracted = MBoxFixture.Extract(text, 0, options);

        var expected = string.Join(terminator, headers) +
                       terminator +
                       terminator +
                       "From body marker" +
                       terminator;
        Assert.That(extracted, Is.EqualTo(expected));
    }
}
