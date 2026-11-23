using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Brows.MBox.Format;

namespace Brows.MBox;

/// <summary>
/// Tests the robustness of the MIME traversal of a body.
/// </summary>
[TestFixture]
internal sealed class MBoxMimeTest {
    [Test]
    public void Body_ReadsAPartThatIsSplitAcrossSmallReads() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "The body.\n" +
            "--b--\n";
        var options = MBoxParserOptions.Default with { BufferSize = 1024 };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers), 0, options);
        Assert.That(message.Body.Text, Is.EqualTo("The body.\n"));
    }

    [Test]
    public void Body_MatchesADelimiterWithTrailingWhitespace() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "--b  \n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "The body.\n" +
            "--b--  \n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Text, Is.EqualTo("The body.\n"));
    }

    [Test]
    public void Body_DoesNotMatchADelimiterWithTrailingContent() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "The body.\n" +
            "--b--x\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(
            message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedMime),
            Is.True);
    }

    [Test]
    public void Body_ReadsAForwardedMessageAsAnOpaqueLeaf() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: message/rfc822\n" +
            "\n" +
            "Subject: The forwarded subject\n" +
            "\n" +
            "The forwarded body.\n" +
            "--b--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Text, Is.Null);
    }

    [Test]
    public void Body_ReadsASignedMessageAsItsFirstContentChild() {
        var headers = new[] {
            "Content-Type: multipart/signed; boundary=\"b\"; protocol=\"application/pkcs7-signature\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "The signed body.\n" +
            "--b\n" +
            "Content-Type: application/pkcs7-signature\n" +
            "\n" +
            "AAAA\n" +
            "--b--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Text, Is.EqualTo("The signed body.\n"));
    }

    [Test]
    public void Body_ReportsAPartThatIsDeeperThanTheLimit() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: multipart/mixed; boundary=\"c\"\n" +
            "\n" +
            "--c\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "The body.\n" +
            "--c--\n" +
            "--b--\n";
        var options = MBoxParserOptions.Default with { MaxMimeDepth = 1 };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers), 0, options);
        Assert.That(
            message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.LimitReached),
            Is.True);
    }

    [Test]
    public void Body_ReadsANestedMultipart() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: multipart/alternative; boundary=\"c\"\n" +
            "\n" +
            "--c\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "The body.\n" +
            "--c--\n" +
            "--b--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Text, Is.EqualTo("The body.\n"));
    }

    [Test]
    public void Body_ReportsTruncatedBase64() {
        var headers = new[] {
            "Content-Type: text/plain; charset=utf-8",
            "Content-Transfer-Encoding: base64",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("VGhlIGJvZHk\n", headers));
        Assert.That(message.Body.Text, Is.EqualTo("The body"));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Malformed));
        Assert.That(
            message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedEncoding),
            Is.True);
    }

    [Test]
    public void Body_ReportsAMalformedQuotedPrintableEscape() {
        var headers = new[] {
            "Content-Type: text/plain; charset=utf-8",
            "Content-Transfer-Encoding: quoted-printable",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("The=ZZbody\n", headers));
        Assert.That(
            message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedEncoding),
            Is.True);
    }

    [Test]
    public void Body_ReportsAnUnsupportedTransferEncoding() {
        var headers = new[] {
            "Content-Type: text/plain; charset=utf-8",
            "Content-Transfer-Encoding: x-uuencode",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("The body.\n", headers));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Unavailable));
        Assert.That(
            message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.UnsupportedBody),
            Is.True);
    }

    [Test]
    public void Body_KeepsTheLaterRecordsOfTheMailboxWhenAPartIsMalformed() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "The body.\n";
        var first = MBoxFixture.Mailbox(body, headers);
        var second = MBoxFixture.Mailbox("The second body.\n");
        var text = first + "\n" + second;
        var records = MBoxFixture.Records(text);
        Assert.That(records.Count, Is.EqualTo(2));
    }

    [Test]
    public void Body_ReportsAHtmlOnlyBody() {
        var headers = new[] {
            "Content-Type: text/html; charset=utf-8",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("<p>The body.</p>\n", headers));
        Assert.That(message.Body.Text, Is.Null);
        Assert.That(message.Body.Html, Is.EqualTo("<p>The body.</p>\n"));
    }

    [Test]
    public void Body_KeepsTheAttachmentOutOfTheSelectedBodyOfAMixedPart() {
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
        Assert.That(message.Body.Parts.Count(item => item.IsAttachmentPart), Is.EqualTo(1));
    }

    [Test]
    public void Body_ReportsThePartsThatWereVisited() {
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
        Assert.That(message.Body.Parts.Count, Is.EqualTo(2));
    }

    [TestCase("\n")]
    [TestCase("\r\n")]
    public void Body_UnfoldsMimePartHeadersBeforeDecoding(string terminator) {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/plain;\n" +
            " charset=windows-1252\n" +
            "Content-Transfer-Encoding:\n" +
            " base64\n" +
            "\n" +
            "6Q==\n" +
            "--b--\n";
        body = body.Replace("\n", terminator, StringComparison.Ordinal);
        var options = MBoxParserOptions.Default with { BufferSize = 1024 };
        var text = MBoxFixture.Mailbox(body, headers, terminator: terminator);
        using var stream = new ChunkedMemoryStream(Encoding.UTF8.GetBytes(text), 7);
        var parser = new MBoxParser(options);
        var records = parser.Scan(stream, null, CancellationToken.None).ToArray();
        var message = parser.ReadMessage(stream, records[0], CancellationToken.None);
        Assert.That(message.Body.Text, Is.EqualTo("é"));
        Assert.That(message.Body.Parts[0].Charset, Is.EqualTo("windows-1252"));
        Assert.That(message.Body.Parts[0].TransferEncoding, Is.EqualTo("base64"));
    }

    [Test]
    public void Body_KeepsAnEncodedMultipartLineLongerThanTheHeaderLimit() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var text = new string('A', 400);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        var body =
            "--b\n" +
            "Content-Type: text/plain; charset=utf-8\n" +
            "Content-Transfer-Encoding: base64\n" +
            "\n" +
            encoded + "\n" +
            "--b--\n";
        var options = MBoxParserOptions.Default with { MaxHeaderBytes = 128 };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers), 0, options);
        Assert.That(message.Body.Text, Is.EqualTo(text));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Complete));
    }

    [Test]
    public void Body_EnforcesTheInputBudgetWhileReadingAnUnbrokenLine() {
        var body = new string('A', 2 * 1024 * 1024) + "TAIL\n";
        var text = MBoxFixture.Mailbox(body);
        var options = MBoxParserOptions.Default with {
            BufferSize = 1024,
            MaxHeaderBytes = 128,
            MaxPreviewInputBytes = 32,
        };
        var parser = new MBoxParser(options);
        using var stream = new CountingMemoryStream(Encoding.UTF8.GetBytes(text));
        var records = parser.Scan(stream, null, CancellationToken.None).ToArray();
        stream.ResetBytesRead();
        var message = parser.ReadMessage(stream, records[0], CancellationToken.None);
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Truncated));
        Assert.That(stream.BytesRead, Is.LessThanOrEqualTo(3 * options.BufferSize));
    }

    [Test]
    public void Body_StopsAtTheConfiguredPartCountAcrossSiblings() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "--b\nContent-Type: text/plain\n\none\n" +
            "--b\nContent-Type: text/plain\n\ntwo\n" +
            "--b\nContent-Type: text/plain\n\nthree\n" +
            "--b--\n";
        var options = MBoxParserOptions.Default with { MaxPartCount = 2 };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers), 0, options);
        Assert.That(message.Body.Parts.Count, Is.EqualTo(2));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Truncated));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.LimitReached), Is.True);
    }

    [Test]
    public void Body_EnforcesThePartCountAcrossNestedParts() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"outer\"",
        };
        var body =
            "--outer\n" +
            "Content-Type: multipart/alternative; boundary=\"inner\"\n" +
            "\n" +
            "--inner\nContent-Type: text/plain\n\none\n" +
            "--inner\nContent-Type: text/plain\n\ntwo\n" +
            "--inner--\n" +
            "--outer\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "three\n" +
            "--outer--\n";
        var options = MBoxParserOptions.Default with { MaxPartCount = 2 };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers), 0, options);
        Assert.That(message.Body.Parts.Count, Is.EqualTo(1));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Truncated));
    }

    [Test]
    public void Body_ExcludesAttachmentDispositionParametersFromMixedSelection() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/plain\n" +
            "Content-Disposition: attachment; size=3\n" +
            "\n" +
            "WRONG\n" +
            "--b\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "RIGHT\n" +
            "--b--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Text, Is.EqualTo("RIGHT\n"));
        Assert.That(message.Body.Parts[0].IsAttachmentPart, Is.True);
    }

    [Test]
    public void Body_PropagatesAttachmentContextThroughNestedMultiparts() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"outer\"",
        };
        var body =
            "--outer\n" +
            "Content-Type: multipart/alternative; boundary=\"inner\"\n" +
            "Content-Disposition: attachment; filename=\"attached.eml\"\n" +
            "\n" +
            "--inner\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "WRONG\n" +
            "--inner--\n" +
            "--outer\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "RIGHT\n" +
            "--outer--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Text, Is.EqualTo("RIGHT\n"));
        Assert.That(message.Body.Parts[0].IsAttachmentPart, Is.True);
        Assert.That(message.Body.Parts[0].FileName, Is.EqualTo("attached.eml"));
    }

    [Test]
    public void Body_UsesContentIdOfANestedMultipartAsTheRelatedRoot() {
        var headers = new[] {
            "Content-Type: multipart/related; boundary=\"outer\"; start=\"<root>\"",
        };
        var body =
            "--outer\n" +
            "Content-Type: multipart/alternative; boundary=\"inner\"\n" +
            "Content-ID: <root>\n" +
            "\n" +
            "--inner\n" +
            "Content-Type: text/html\n" +
            "\n" +
            "<p>ROOT</p>\n" +
            "--inner--\n" +
            "--outer\n" +
            "Content-Type: text/html\n" +
            "Content-ID: <other>\n" +
            "\n" +
            "<p>OTHER</p>\n" +
            "--outer--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Html, Is.EqualTo("<p>ROOT</p>\n"));
    }

    [Test]
    public void Body_DoesNotSelectASinglePartAttachmentAsTheMessageBody() {
        var headers = new[] {
            "Content-Type: text/plain",
            "Content-Disposition: attachment; size=3",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("WRONG\n", headers));
        Assert.That(message.Body.Text, Is.Null);
        Assert.That(message.Body.Parts[0].IsAttachmentPart, Is.True);
    }

    [Test]
    public void Body_RecoversAnOuterSiblingWhenAnInnerMultipartLacksItsClosingBoundary() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"outer\"",
        };
        var body =
            "--outer\n" +
            "Content-Type: multipart/mixed; boundary=\"inner\"\n" +
            "\n" +
            "--inner\n" +
            "Content-Type: application/octet-stream\n" +
            "\n" +
            "AAAA\n" +
            "--outer\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "RIGHT\n" +
            "--outer--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Text, Is.EqualTo("RIGHT\n"));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Complete));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedMime), Is.True);
    }

    [Test]
    public void Body_RecoversTheOuterFinalBoundaryAcrossMultipleMissingMultipartCloses() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"outer\"",
        };
        var body =
            "--outer\n" +
            "Content-Type: multipart/mixed; boundary=\"middle\"\n" +
            "\n" +
            "--middle\n" +
            "Content-Type: multipart/mixed; boundary=\"inner\"\n" +
            "\n" +
            "--inner\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "VISIBLE\n" +
            "--outer--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Text, Is.EqualTo("VISIBLE\n"));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Malformed));
        Assert.That(message.Diagnostics.Count(item => item.Code == MBoxDiagnosticCode.MalformedMime), Is.EqualTo(2));
    }

    [Test]
    public void Body_ReportsIncompleteBase64AsMalformedWhileKeepingRecoveredText() {
        var headers = new[] {
            "Content-Type: text/plain; charset=utf-8",
            "Content-Transfer-Encoding: base64",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("VGhlIGJvZHk\n", headers));
        Assert.That(message.Body.Text, Is.EqualTo("The body"));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Malformed));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedEncoding), Is.True);
    }

    [Test]
    public void Body_ReportsInvalidBase64PaddingAsMalformed() {
        var headers = new[] {
            "Content-Type: text/plain; charset=utf-8",
            "Content-Transfer-Encoding: base64",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox("VGhlIGJvZHk=AAAA\n", headers));
        Assert.That(message.Body.Text, Is.EqualTo("The body"));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Malformed));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedEncoding), Is.True);
    }

    [Test]
    public void Body_LeavesTheSelectedStateCompleteWhenOnlyAnUnselectedSiblingIsMalformed() {
        var headers = new[] {
            "Content-Type: multipart/mixed; boundary=\"b\"",
        };
        var body =
            "--b\n" +
            "Content-Type: text/plain\n" +
            "\n" +
            "RIGHT\n" +
            "--b\n" +
            "Content-Type: text/plain\n" +
            "Content-Transfer-Encoding: base64\n" +
            "\n" +
            "not base64!!\n" +
            "--b--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Text, Is.EqualTo("RIGHT\n"));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Complete));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedEncoding), Is.True);
    }

    [Test]
    public void Body_ReadsATakeoutShapedMessageWithFoldedHeadersAndAttachments() {
        var headers = new[] {
            "From: Someone <someone@example.com>",
            "Subject: Takeout sample",
            "X-Gmail-Labels: Inbox, Important",
            "X-GM-THRID: 1234567890",
            "Content-Type: multipart/mixed;",
            " boundary=\"outer\"",
        };
        var body =
            "--outer\n" +
            "Content-Type: multipart/alternative;\n" +
            " boundary=\"alternative\"\n" +
            "\n" +
            "--alternative\n" +
            "Content-Type: text/plain;\n" +
            " charset=utf-8\n" +
            "\n" +
            "Takeout plain text.\n" +
            "--alternative\n" +
            "Content-Type: text/html; charset=utf-8\n" +
            "\n" +
            "<p>Takeout HTML.</p>\n" +
            "--alternative--\n" +
            "--outer\n" +
            "Content-Type: application/octet-stream\n" +
            "Content-Disposition: attachment;\n" +
            " filename=\"image.bin\"\n" +
            "Content-Transfer-Encoding:\n" +
            " base64\n" +
            "\n" +
            "AQID\n" +
            "--outer--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Headers.Get("X-Gmail-Labels")?.RawValue, Is.EqualTo("Inbox, Important"));
        Assert.That(message.Headers.Get("X-GM-THRID")?.RawValue, Is.EqualTo("1234567890"));
        Assert.That(message.Body.Text, Is.EqualTo("Takeout plain text.\n"));
        Assert.That(message.Body.Html, Is.EqualTo("<p>Takeout HTML.</p>\n"));
        Assert.That(message.Body.Parts.Count, Is.EqualTo(3));
        Assert.That(message.Body.Parts[^1].IsAttachmentPart, Is.True);
        Assert.That(message.Body.Parts[^1].FileName, Is.EqualTo("image.bin"));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Complete));
    }

    [Test]
    public void Body_ReadsAnHtmlOnlyTakeoutMessageWithAnAttachment() {
        var headers = new[] {
            "From: Someone <someone@example.com>",
            "Subject: Takeout HTML sample",
            "X-Gmail-Labels: Inbox, Starred",
            "X-GM-THRID: 9876543210",
            "Content-Type: multipart/mixed;",
            " boundary=\"outer\"",
        };
        var body =
            "--outer\n" +
            "Content-Type: text/html;\n" +
            " charset=utf-8\n" +
            "\n" +
            "<p>Takeout HTML only.</p>\n" +
            "--outer\n" +
            "Content-Type: application/octet-stream\n" +
            "Content-Disposition: attachment; filename=\"photo.bin\"\n" +
            "Content-Transfer-Encoding: base64\n" +
            "\n" +
            "AQID\n" +
            "--outer--\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Headers.Get("X-Gmail-Labels")?.RawValue, Is.EqualTo("Inbox, Starred"));
        Assert.That(message.Headers.Get("X-GM-THRID")?.RawValue, Is.EqualTo("9876543210"));
        Assert.That(message.Body.Text, Is.Null);
        Assert.That(message.Body.Html, Is.EqualTo("<p>Takeout HTML only.</p>\n"));
        Assert.That(message.Body.Parts.Count, Is.EqualTo(2));
        Assert.That(message.Body.Parts[1].IsAttachmentPart, Is.True);
        Assert.That(message.Body.Parts[1].FileName, Is.EqualTo("photo.bin"));
    }

    private sealed class CountingMemoryStream : MemoryStream {
        public long BytesRead { get; private set; }

        public CountingMemoryStream(byte[] bytes) : base(bytes) {
        }

        public override int Read(byte[] buffer, int offset, int count) {
            var read = base.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public void ResetBytesRead() {
            BytesRead = 0;
        }
    }

    private sealed class ChunkedMemoryStream : MemoryStream {
        private readonly int _MaximumRead;

        public ChunkedMemoryStream(byte[] bytes, int maximumRead) : base(bytes) {
            _MaximumRead = maximumRead;
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return base.Read(buffer, offset, Math.Min(count, _MaximumRead));
        }
    }
}
