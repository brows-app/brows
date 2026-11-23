using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Brows.MBox.Format;

namespace Brows.MBox;

[TestFixture]
internal sealed class MBoxParserTest {
    private const string TextPart =
        "Content-Type: text/plain; charset=utf-8\n" +
        "\n" +
        "Body text.\n";

    private static List<MBoxAttachment> Attachments(string text, MBoxParserOptions options = null) {
        var parser = new MBoxParser(options ?? MBoxParserOptions.Default);
        using var stream = MBoxFixture.Stream(text);
        var records = new List<MBoxRecord>(parser.Scan(stream, null, CancellationToken.None));
        var attachments = new List<MBoxAttachment>();
        parser.ExtractAttachments(stream, records[0], attachments.Add, CancellationToken.None);
        return attachments;
    }

    private static bool Extract(string text, MBoxParserOptions options) {
        var parser = new MBoxParser(options);
        using var stream = MBoxFixture.Stream(text);
        var records = new List<MBoxRecord>(parser.Scan(stream, null, CancellationToken.None));
        return parser.ExtractAttachments(stream, records[0], _ => { }, CancellationToken.None);
    }

    private static string Attached(string encoding, string payload) {
        return "Content-Type: application/octet-stream\n" +
               "Content-Disposition: attachment; filename=\"exact.bin\"\n" +
               $"Content-Transfer-Encoding: {encoding}\n" +
               "\n" +
               payload + "\n";
    }

    private static string Mixed(params string[] parts) {
        var body = new StringBuilder();
        foreach (var part in parts) {
            body.Append("--mixed\n");
            body.Append(part);
        }
        body.Append("--mixed--\n");
        return MBoxFixture.Mailbox(body.ToString(), [
            "Subject: Attachments",
            "Content-Type: multipart/mixed; boundary=\"mixed\"",
        ]);
    }

    [Test]
    public void ExtractAttachments_DecodesTheAttachmentsInTheirOrder() {
        var attachments = Attachments(Mixed(
            TextPart,
            "Content-Type: application/octet-stream\n" +
            "Content-Disposition: attachment; filename=\"first.bin\"\n" +
            "Content-Transfer-Encoding: base64\n" +
            "\n" +
            "QVRUQUNI\n" +
            "TUVOVC1PTkU=\n",
            "Content-Type: text/plain; charset=utf-8; name=\"second.txt\"\n" +
            "Content-Transfer-Encoding: quoted-printable\n" +
            "\n" +
            "Caf=C3=A9 =3D soft=\n" +
            " break\n",
            "Content-Type: text/csv\n" +
            "Content-Disposition: attachment; filename=\"third.csv\"\n" +
            "\n" +
            "a,b\n" +
            "1,2\n"));

        Assert.That(attachments.Select(item => item.Part.FileName),
                    Is.EqualTo(new[] { "first.bin", "second.txt", "third.csv" }));
        using (Assert.EnterMultipleScope()) {
            Assert.That(Encoding.ASCII.GetString(attachments[0].Bytes), Is.EqualTo("ATTACHMENT-ONE"));
            Assert.That(Encoding.UTF8.GetString(attachments[1].Bytes), Is.EqualTo("Café = soft break"));
            Assert.That(Encoding.ASCII.GetString(attachments[2].Bytes), Is.EqualTo("a,b\n1,2"));
            Assert.That(attachments.Select(item => item.IsComplete), Is.All.True);
        }
    }

    [Test]
    public void ExtractAttachments_ReportsNothingForAMessageWithoutAttachments() {
        Assert.That(Attachments(Mixed(TextPart)), Is.Empty);
        Assert.That(Attachments(MBoxFixture.Mailbox("Only text.\n")), Is.Empty);
    }

    [Test]
    public void ExtractAttachments_ReadsTheWholeAttachmentWhateverThePreviewInputLimit() {
        var payload = new string('x', 4096);
        var base64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(payload));
        var lines = string.Concat(base64.Chunk(76).Select(chunk => new string(chunk) + "\n"));
        var options = MBoxParserOptions.Default with { MaxPreviewInputBytes = 64 };
        var attachments = Attachments(Mixed(
            TextPart,
            "Content-Type: application/octet-stream\n" +
            "Content-Disposition: attachment; filename=\"large.bin\"\n" +
            "Content-Transfer-Encoding: base64\n" +
            "\n" +
            lines), options);

        Assert.That(attachments, Has.Count.EqualTo(1));
        Assert.That(attachments[0].IsComplete, Is.True);
        Assert.That(Encoding.ASCII.GetString(attachments[0].Bytes), Is.EqualTo(payload));
    }

    [Test]
    public void ExtractAttachments_RemovesTheQuotingOfTheMailboxFromTheAttachment() {
        var attachments = Attachments(Mixed(
            "Content-Type: text/plain\n" +
            "Content-Disposition: attachment; filename=\"quoted.txt\"\n" +
            "\n" +
            ">From the start of a line.\n"));

        Assert.That(Encoding.ASCII.GetString(attachments[0].Bytes), Is.EqualTo("From the start of a line."));
    }

    [TestCase("7bit", "\n")]
    [TestCase("7bit", "\r\n")]
    [TestCase("8bit", "\r\n")]
    [TestCase("binary", "\r\n")]
    [TestCase("quoted-printable", "\n")]
    [TestCase("quoted-printable", "\r\n")]
    public void ExtractAttachments_KeepsTheLineBreaksOfTheAttachment(string encoding, string lineBreak) {
        var attachments = Attachments(Mixed(Attached(encoding, "AB\nCD")).Replace("\n", lineBreak));

        Assert.That(attachments[0].Bytes, Is.EqualTo(Encoding.ASCII.GetBytes($"AB{lineBreak}CD")));
    }

    [TestCase("7bit", "\n")]
    [TestCase("7bit", "\r\n")]
    [TestCase("quoted-printable", "\r\n")]
    public void ExtractAttachments_KeepsTheFinalLineBreakOfTheAttachment(string encoding, string lineBreak) {
        var attachments = Attachments(Mixed(Attached(encoding, "AB\nCD\n")).Replace("\n", lineBreak));

        Assert.That(attachments[0].Bytes, Is.EqualTo(Encoding.ASCII.GetBytes($"AB{lineBreak}CD{lineBreak}")));
    }

    [Test]
    public void ExtractAttachments_KeepsTheBytesOfABinaryAttachment() {
        var payload = "A\rB\0Cÿ";
        var attachments = Attachments(Mixed(Attached("binary", payload)));

        Assert.That(attachments[0].Bytes, Is.EqualTo(Encoding.UTF8.GetBytes(payload)));
    }

    [TestCase("\n")]
    [TestCase("\r\n")]
    public void ExtractAttachments_JoinsTheSoftLineBreaksOfAQuotedPrintableAttachment(string lineBreak) {
        var attachments = Attachments(Mixed(Attached("quoted-printable", "AB=\nCD")).Replace("\n", lineBreak));

        Assert.That(attachments[0].Bytes, Is.EqualTo(Encoding.ASCII.GetBytes("ABCD")));
    }

    [Test]
    public void ExtractAttachments_ReturnsTrueWhenEveryAttachmentIsRead() {
        var text = Mixed(Attached("7bit", "first"), Attached("7bit", "second"));

        Assert.That(Extract(text, MBoxParserOptions.Default), Is.True);
    }

    [Test]
    public void ExtractAttachments_ReturnsFalseWhenThePartLimitStopsTheRead() {
        var text = Mixed(Attached("7bit", "first"), Attached("7bit", "second"), Attached("7bit", "third"));
        var options = MBoxParserOptions.Default with { MaxPartCount = 2 };

        Assert.That(Extract(text, options), Is.False);
    }

    [Test]
    public void ExtractAttachments_ReturnsTrueWhenOnlyTheLimitOfTheDisplayedTextIsReached() {
        var text = Mixed(TextPart, Attached("7bit", "attached"));
        var options = MBoxParserOptions.Default with { MaxPreviewCharacters = 1 };

        Assert.That(Extract(text, options), Is.True);
    }

    [Test]
    public void ExtractAttachments_ReportsNoBytesForAnUnsupportedTransferEncoding() {
        var attachments = Attachments(Mixed(
            "Content-Type: application/octet-stream\n" +
            "Content-Disposition: attachment; filename=\"packed.bin\"\n" +
            "Content-Transfer-Encoding: x-uuencode\n" +
            "\n" +
            "begin 644 packed.bin\n"));

        Assert.That(attachments, Has.Count.EqualTo(1));
        Assert.That(attachments[0].Bytes, Is.Null);
    }

    [Test]
    public void ExtractAttachments_ReportsAnAttachmentThatTheMailboxCutsShortAsIncomplete() {
        var text = MBoxFixture.Mailbox(
            "--mixed\n" +
            "Content-Type: application/octet-stream\n" +
            "Content-Disposition: attachment; filename=\"cut.bin\"\n" +
            "Content-Transfer-Encoding: base64\n" +
            "\n" +
            "QVRUQUNI\n", [
                "Subject: Cut short",
                "Content-Type: multipart/mixed; boundary=\"mixed\"",
            ]);
        var attachments = Attachments(text);

        Assert.That(attachments, Has.Count.EqualTo(1));
        Assert.That(attachments[0].IsComplete, Is.False);
    }

    [Test]
    public void Scan_EmptyMailbox_ReturnsNoRecords() {
        var (records, diagnostics) = MBoxFixture.Scan(string.Empty);
        Assert.That(records, Is.Empty);
        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public void Scan_EnvelopeOnly_ReturnsOneRecord() {
        var text = MBoxFixture.Envelope + "\n";
        var (records, diagnostics) = MBoxFixture.Scan(text);
        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(diagnostics, Is.Empty);
        Assert.That(records[0].Offset, Is.EqualTo(0));
        Assert.That(records[0].MessageOffset, Is.EqualTo(text.Length));
    }

    [Test]
    public void Scan_InvalidPrefix_ReportsADiagnosticAndNoRecords() {
        var (records, diagnostics) = MBoxFixture.Scan("This is not a mailbox.\n");
        Assert.That(records, Is.Empty);
        Assert.That(diagnostics, Has.Count.EqualTo(1));
        Assert.That(diagnostics[0].Code, Is.EqualTo(MBoxDiagnosticCode.MalformedEnvelope));
    }

    [Test]
    public void Scan_TwoMessages_ReturnsTwoRecords() {
        var text = MBoxFixture.Mailbox(["First body.\n", "Second body.\n"]);
        var records = MBoxFixture.Records(text);
        Assert.That(records, Has.Count.EqualTo(2));
        Assert.That(records[0].Offset, Is.EqualTo(0));
        Assert.That(records[1].Offset, Is.EqualTo(text.IndexOf(MBoxFixture.Envelope, 1, StringComparison.Ordinal)));
    }

    [Test]
    public void Scan_RecordLength_EndsAtTheNextSeparator() {
        var text = MBoxFixture.Mailbox(["First body.\n", "Second body.\n"]);
        var records = MBoxFixture.Records(text);
        Assert.That(records[0].Length, Is.EqualTo(records[1].Offset));
        Assert.That(records[1].Length, Is.EqualTo(text.Length - records[1].Offset));
    }

    [Test]
    public void Scan_SeparatorAfterANonEmptyLine_IsNotASeparator() {
        /*
         * A separator is only recognized at the start of a line that follows an actually empty
         * line, so ordinary prose that begins with "From " is kept as body data.
         */
        var body = "First body.\n" + MBoxFixture.Envelope + "\n";
        var text = MBoxFixture.Mailbox(body);
        var records = MBoxFixture.Records(text);
        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0].Length, Is.EqualTo(text.Length));
    }

    [Test]
    public void Scan_SeparatorAfterAnEmptyLine_IsASeparator() {
        var text = MBoxFixture.Mailbox("First body.\n\n") +
                   MBoxFixture.Mailbox("Second body.\n");
        var records = MBoxFixture.Records(text);
        Assert.That(records, Has.Count.EqualTo(2));
    }

    [Test]
    public void Scan_TwoMessagesFramedByAnEmptyLine_AreTwoRecords() {
        /*
         * The empty line that separates two records is framing, so it belongs to neither
         * message.
         */
        var text = MBoxFixture.Mailbox(["First body.\n", "Second body.\n"]);
        var records = MBoxFixture.Records(text);
        Assert.That(records, Has.Count.EqualTo(2));
        /*
         * A message is the empty line that ends its header block, followed by its body. The
         * empty line that frames the record is not part of the message.
         */
        Assert.That(records[0].MessageLength, Is.EqualTo("\n".Length + "First body.\n".Length));
        Assert.That(records[1].MessageLength, Is.EqualTo("\n".Length + "Second body.\n".Length));
    }

    [Test]
    public void Scan_ProseBeginningWithFrom_IsKeptAsBodyData() {
        var body = "From here on the text is prose.\n";
        var text = MBoxFixture.Mailbox(body);
        var records = MBoxFixture.Records(text);
        Assert.That(records, Has.Count.EqualTo(1));
    }

    [Test]
    public void Scan_CrlfTerminators_AreSupported() {
        var text = MBoxFixture.Mailbox("First body.\n", terminator: "\r\n");
        var records = MBoxFixture.Records(text);
        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0].MessageOffset, Is.EqualTo(MBoxFixture.Envelope.Length + 2));
    }

    [Test]
    public void Scan_MixedTerminators_AreSupported() {
        var text = MBoxFixture.Envelope + "\r\n" + "Subject: A subject\n" + "\r\n" + "Body.\n";
        var records = MBoxFixture.Records(text);
        Assert.That(records, Has.Count.EqualTo(1));
    }

    [Test]
    public void Scan_GoogleEnvelopeDate_IsParsed() {
        var records = MBoxFixture.Records(MBoxFixture.Mailbox("Body.\n"));
        var date = records[0].Envelope.Date;
        Assert.That(date.HasValue, Is.True);
        Assert.That(date.Value, Is.EqualTo(new DateTimeOffset(2015, 9, 17, 18, 9, 44, TimeSpan.Zero)));
    }

    [Test]
    public void Scan_ClassicAsctimeEnvelope_IsReadAsUtc() {
        var envelope = "From sender@example.com Thu Sep 17 18:09:44 2015";
        var records = MBoxFixture.Records(MBoxFixture.Mailbox("Body.\n", envelope: envelope));
        var date = records[0].Envelope.Date;
        Assert.That(date.HasValue, Is.True);
        Assert.That(date.Value.Offset, Is.EqualTo(TimeSpan.Zero));
        Assert.That(date.Value, Is.EqualTo(new DateTimeOffset(2015, 9, 17, 18, 9, 44, TimeSpan.Zero)));
    }

    [Test]
    public void Scan_Rfc2822Envelope_IsParsed() {
        var envelope = "From sender@example.com Thu, 17 Sep 2015 17:54:37 +0000";
        var records = MBoxFixture.Records(MBoxFixture.Mailbox("Body.\n", envelope: envelope));
        var date = records[0].Envelope.Date;
        Assert.That(date.HasValue, Is.True);
        Assert.That(date.Value, Is.EqualTo(new DateTimeOffset(2015, 9, 17, 17, 54, 37, TimeSpan.Zero)));
    }

    [Test]
    public void Scan_EnvelopeWithAnImpossibleDate_KeepsTheRecordAndReportsADiagnostic() {
        var envelope = "From sender@example.com Thu Feb 30 18:09:44 +0000 2015";
        var (records, diagnostics) = MBoxFixture.Scan(MBoxFixture.Mailbox("Body.\n", envelope: envelope));
        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0].Envelope.Date.HasValue, Is.False);
        Assert.That(diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedDate), Is.True);
    }

    [Test]
    public void Scan_EnvelopeWithAMismatchedWeekday_ReportsADiagnosticAndKeepsTheDate() {
        var envelope = "From sender@example.com Wed Sep 17 18:09:44 +0000 2015";
        var (records, diagnostics) = MBoxFixture.Scan(MBoxFixture.Mailbox("Body.\n", envelope: envelope));
        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0].Envelope.Date.HasValue, Is.True);
        Assert.That(diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedDate), Is.True);
    }

    [Test]
    public void Scan_NullSender_IsAnEmptySender() {
        var envelope = "From - Thu Sep 17 18:09:44 +0000 2015";
        var records = MBoxFixture.Records(MBoxFixture.Mailbox("Body.\n", envelope: envelope));
        Assert.That(records[0].Envelope.Sender, Is.Empty);
    }

    [Test]
    public void Scan_UnknownDateTailWithAThreadId_IsRecovered() {
        var envelope = "From sender@example.com unknown date";
        var headers = new[] {
            "X-GM-THRID: 1512584914615853097",
            "Subject: A subject",
        };
        var (records, diagnostics) = MBoxFixture.Scan(MBoxFixture.Mailbox("Body.\n", headers, envelope));
        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0].Envelope.Date.HasValue, Is.False);
        Assert.That(diagnostics.Any(item => item.Code == MBoxDiagnosticCode.RecoveredRecord), Is.True);
    }

    [Test]
    public void Scan_UnknownDateTailWithFromAndSubject_IsRecovered() {
        var envelope = "From sender@example.com unknown date";
        var headers = new[] {
            "From: Someone <someone@example.com>",
            "Subject: A subject",
        };
        var (records, diagnostics) = MBoxFixture.Scan(MBoxFixture.Mailbox("Body.\n", headers, envelope));
        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(diagnostics.Any(item => item.Code == MBoxDiagnosticCode.RecoveredRecord), Is.True);
    }

    [TestCase(false, 16)]
    [TestCase(false, 2048)]
    [TestCase(true, 16)]
    [TestCase(true, 2048)]
    public void Scan_RecoveryKeepsTheOriginalEnvelopeBytes(bool afterAValidRecord, int fillLength) {
        var envelope = "From sender@example.com unknown date";
        var headers = new[] {
            "X-Fill: " + new string('x', fillLength),
            "From: Someone <someone@example.com>",
            "Subject: A subject",
        };
        var recovered = MBoxFixture.Mailbox("Recovered body.\n", headers, envelope);
        var text = afterAValidRecord
            ? MBoxFixture.Mailbox("First body.\n") + "\n" + recovered
            : recovered;

        var records = MBoxFixture.Records(text);
        var record = records[afterAValidRecord ? 1 : 0];

        Assert.That(records, Has.Count.EqualTo(afterAValidRecord ? 2 : 1));
        Assert.That(record.Envelope.Sender, Is.EqualTo("sender@example.com"));
        Assert.That(record.Envelope.Raw, Is.EqualTo(envelope));
    }

    [Test]
    public void Scan_UnknownDateTailWithoutEnoughHeaders_IsKeptAsBodyData() {
        var envelope = "From sender@example.com unknown date";
        var headers = new[] {
            "Subject: A subject",
        };
        var (records, diagnostics) = MBoxFixture.Scan(MBoxFixture.Mailbox("Body.\n", headers, envelope));
        Assert.That(records, Is.Empty);
        Assert.That(diagnostics.Any(item => item.Code == MBoxDiagnosticCode.AmbiguousRecovery), Is.True);
    }

    [Test]
    public void Scan_InteriorCandidateThatCannotBeRecovered_IsKeptAsBodyData() {
        var body = "First body.\n\n" + "From sender@example.com unknown date\n" + "Subject: A subject\n";
        var text = MBoxFixture.Mailbox(body);
        var (records, diagnostics) = MBoxFixture.Scan(text);
        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(diagnostics.Any(item => item.Code == MBoxDiagnosticCode.AmbiguousRecovery), Is.True);
    }

    [Test]
    public void Scan_ContentLengthHeader_IsNeverUsedForFraming() {
        /*
         * A Content-Length header that points at a later record must not change where the
         * records end.
         */
        var headers = new[] {
            "Content-Length: 5000",
        };
        var text = MBoxFixture.Mailbox(["First body.\n", "Second body.\n"], headers);
        var records = MBoxFixture.Records(text);
        Assert.That(records, Has.Count.EqualTo(2));
    }

    [Test]
    public void Scan_ReadsEveryLineAcrossSmallBuffers() {
        var text = MBoxFixture.Mailbox("A body that is long enough to cross a buffer.\n");
        var options = MBoxParserOptions.Default with { BufferSize = 1024 };
        var records = MBoxFixture.Records(text, options);
        Assert.That(records, Has.Count.EqualTo(1));
    }

    [Test]
    public void ReadHeaders_ReadsOnlyTheHeaderBlock() {
        var headers = new[] {
            "Subject: A subject",
            "From: Someone <someone@example.com>",
        };
        var text = MBoxFixture.Mailbox("The body.\n", headers);
        var message = MBoxFixture.Headers(text);
        Assert.That(message.Headers.Names, Is.EqualTo(new[] { "Subject", "From" }));
        Assert.That(message.Headers["Subject"].Value, Is.EqualTo("A subject"));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.NotRead));
        Assert.That(message.Body.Text, Is.Null);
    }

    [Test]
    public void ReadHeaders_FoldedFieldsShareTheAggregateByteLimit() {
        var headers = new[] {
            "Subject: A",
        }.Concat(Enumerable.Repeat(" continued", 100));
        var options = MBoxParserOptions.Default with { MaxHeaderBytes = 32 };

        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers), 0, options);

        Assert.That(message.Headers["Subject"].RawValue.Length, Is.LessThanOrEqualTo(32));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.HeaderLimitReached), Is.True);
    }

    [Test]
    public void ReadHeaders_CancellationInterruptsFoldUnfolding() {
        var headers = new[] {
            "Subject: A",
        }.Concat(Enumerable.Repeat(" continued", 100));
        var text = MBoxFixture.Mailbox("Body.\n", headers);
        var parser = new MBoxParser(MBoxParserOptions.Default with { BufferSize = 1024 });
        using var scanStream = MBoxFixture.Stream(text);
        var record = parser.Scan(scanStream, null, CancellationToken.None).Single();
        using var cancellation = new CancellationTokenSource();
        var cancelAt = checked((int)record.MessageOffset + 24);
        using var stream = new CancellingReadStream(Encoding.UTF8.GetBytes(text), cancelAt, cancellation, 1);

        Assert.That(
            () => parser.ReadHeaders(stream, record, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(stream.Position, Is.LessThan(cancelAt + 2));
    }

    [Test]
    public void ReadHeaders_CancellationInterruptsRecoveryLookahead() {
        var envelope = "From sender@example.com unknown date";
        var headers = new[] {
            "From: Someone <someone@example.com>",
            "Subject: A subject",
        }.Concat(Enumerable.Repeat("X-Fill: value", 100));
        var text = MBoxFixture.Mailbox("Body.\n", headers, envelope);
        using var cancellation = new CancellationTokenSource();
        var cancelAt = Encoding.UTF8.GetByteCount(envelope + "\n") + 32;
        using var stream = new CancellingReadStream(Encoding.UTF8.GetBytes(text), cancelAt, cancellation, 1);
        var parser = new MBoxParser(MBoxParserOptions.Default with { BufferSize = 1024 });

        Assert.That(
            () => new List<MBoxRecord>(parser.Scan(stream, null, cancellation.Token)),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(stream.Position, Is.LessThan(cancelAt + 2));
    }

    [Test]
    public void ReadMessage_HeaderCountLimitDoesNotTreatUnparsedHeadersAsBody() {
        var headers = new[] {
            "Subject: A subject",
            "Content-Type: text/plain",
            "X-Internal: secret",
        };
        var options = MBoxParserOptions.Default with { MaxHeaderCount = 1 };

        var message = MBoxFixture.Message(MBoxFixture.Mailbox("Body marker.\n", headers), 0, options);

        Assert.That(message.Headers.Names, Is.EqualTo(new[] { "Subject" }));
        Assert.That(message.Body.Text, Is.Null);
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Truncated));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.HeaderLimitReached), Is.True);
    }

    [Test]
    public void ReadMessage_HeaderByteLimitDoesNotTreatTheHeaderSuffixAsBody() {
        var headers = new[] {
            "Subject: A subject",
            "X-Internal: secret",
        };
        var options = MBoxParserOptions.Default with { MaxHeaderBytes = 16 };

        var message = MBoxFixture.Message(MBoxFixture.Mailbox("Body marker.\n", headers), 0, options);

        Assert.That(message.Body.Text, Is.Null);
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Truncated));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.HeaderLimitReached), Is.True);
    }

    [Test]
    public void ReadMessage_ReadsTheBody() {
        var text = MBoxFixture.Mailbox("The body.\n");
        var message = MBoxFixture.Message(text);
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Complete));
        Assert.That(message.Body.Text, Is.EqualTo("The body.\n"));
    }

    [Test]
    public void ReadMessage_AnEmptyBodyIsAnEmptyText() {
        var text = MBoxFixture.Mailbox(string.Empty);
        var message = MBoxFixture.Message(text);
        Assert.That(message.Body.Text, Is.EqualTo(string.Empty));
        Assert.That(message.Body.State, Is.EqualTo(MBoxBodyState.Complete));
    }

    [Test]
    public void Scan_AStreamThatIsNotSeekable_IsRejected() {
        using var stream = new NonSeekableStream(Encoding.UTF8.GetBytes(MBoxFixture.Mailbox("Body.\n")));
        var parser = new MBoxParser(MBoxParserOptions.Default);
        Assert.That(() => parser.Scan(stream, null, CancellationToken.None), Throws.ArgumentException);
    }

    [Test]
    public void Scan_LeavesTheSourceOpen() {
        using var stream = MBoxFixture.Stream(MBoxFixture.Mailbox("Body.\n"));
        var parser = new MBoxParser(MBoxParserOptions.Default);
        var records = new List<MBoxRecord>(parser.Scan(stream, null, CancellationToken.None));
        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(stream.CanRead, Is.True);
    }

    /// <summary>
    /// A stream that cannot seek.
    /// </summary>
    private sealed class NonSeekableStream : MemoryStream {
        public NonSeekableStream(byte[] buffer) : base(buffer) {
        }

        public override bool CanSeek =>
            false;

        public override long Seek(long offset, SeekOrigin loc) {
            throw new NotSupportedException();
        }
    }

    private sealed class CancellingReadStream : MemoryStream {
        private readonly CancellationTokenSource _Cancellation;
        private readonly int _CancelAt;
        private readonly int _MaxRead;

        public CancellingReadStream(byte[] buffer,
                                    int cancelAt,
                                    CancellationTokenSource cancellation,
                                    int maxRead) : base(buffer) {
            _CancelAt = cancelAt;
            _Cancellation = cancellation;
            _MaxRead = maxRead;
        }

        public override int Read(byte[] buffer, int offset, int count) {
            var read = base.Read(buffer, offset, Math.Min(count, _MaxRead));
            if (Position >= _CancelAt) {
                _Cancellation.Cancel();
            }
            return read;
        }
    }
}
