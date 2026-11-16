using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Brows.MBox.Format;

namespace Brows.MBox;

/// <summary>
/// Tests the extents of the records of a mailbox, and the way they are read.
/// </summary>
[TestFixture]
public sealed class MBoxExtentsTest {
    [Test]
    public void Scan_ReportsTheExtentsOfARecord() {
        var text = MBoxFixture.Mailbox("The body.\n");
        var records = MBoxFixture.Records(text);
        Assert.That(records.Count, Is.EqualTo(1));
        var record = records[0];
        Assert.That(record.Offset, Is.EqualTo(0));
        Assert.That(record.Length, Is.EqualTo((long)text.Length));
        Assert.That(record.MessageOffset, Is.EqualTo((long)(MBoxFixture.Envelope.Length + 1)));
        Assert.That(record.MessageLength, Is.EqualTo((long)("\nThe body.\n".Length)));
    }

    [Test]
    public void Scan_ReportsTheBodyOffsetOfARecord() {
        var headers = new[] {
            "Subject: A subject",
        };
        var text = MBoxFixture.Mailbox("The body.\n", headers);
        var records = MBoxFixture.Records(text);
        var expected = (long)(MBoxFixture.Envelope.Length + 1 + "Subject: A subject\n".Length + 1);
        Assert.That(records[0].BodyOffset, Is.EqualTo(expected));
    }

    [Test]
    public void Scan_ReportsNoBodyOffsetWhenTheHeaderBlockIsNotTerminated() {
        var text = MBoxFixture.Envelope + "\nSubject: A subject\n";
        var records = MBoxFixture.Records(text);
        Assert.That(records.Count, Is.EqualTo(1));
        Assert.That(records[0].BodyOffset, Is.Null);
    }

    [Test]
    public void Scan_ExcludesTheFramingOfTheLastRecord() {
        var text = MBoxFixture.Mailbox(["First.\n", "Second.\n"]);
        var records = MBoxFixture.Records(text);
        Assert.That(records.Count, Is.EqualTo(2));
        Assert.That(records[0].MessageLength, Is.EqualTo((long)("\nFirst.\n".Length)));
        Assert.That(records[1].MessageLength, Is.EqualTo((long)("\nSecond.\n".Length)));
    }

    [Test]
    public void Scan_KeepsTheExtraBlankLinesOfARecord() {
        var text = MBoxFixture.Mailbox("First.\n\n\nSecond.\n");
        var records = MBoxFixture.Records(text);
        Assert.That(records.Count, Is.EqualTo(1));
        Assert.That(records[0].MessageLength, Is.EqualTo((long)("\nFirst.\n\n\nSecond.\n".Length)));
    }

    [Test]
    public void Scan_ReadsAMessageThatIsNotTerminated() {
        var text = MBoxFixture.Mailbox("The body.");
        var records = MBoxFixture.Records(text);
        Assert.That(records.Count, Is.EqualTo(1));
        Assert.That(records[0].MessageLength, Is.EqualTo((long)("\nThe body.".Length)));
    }

    [Test]
    public void Scan_ReadsEveryLineAcrossSmallBuffers() {
        var builder = new StringBuilder();
        for (var i = 0; i < 500; i++) {
            builder.Append("The body of the message.\n");
        }
        var text = MBoxFixture.Mailbox(builder.ToString());
        var options = MBoxParserOptions.Default with { BufferSize = 1024 };
        var records = MBoxFixture.Records(text, options);
        Assert.That(records.Count, Is.EqualTo(1));
        Assert.That(records[0].MessageLength, Is.EqualTo((long)(builder.Length + 1)));
    }

    [Test]
    public void Scan_ReadsALineThatIsLongerThanTheBuffer() {
        var line = new string('x', 300 * 1024);
        var text = MBoxFixture.Mailbox(line + "\n");
        var options = MBoxParserOptions.Default with { BufferSize = 1024 };
        var records = MBoxFixture.Records(text, options);
        Assert.That(records.Count, Is.EqualTo(1));
        Assert.That(records[0].MessageLength, Is.EqualTo((long)(line.Length + 2)));
    }

    [Test]
    public void Scan_ReadsAMixedTerminatorMailbox() {
        var text = MBoxFixture.Envelope + "\nSubject: A subject\r\n\r\nThe body.\n";
        var records = MBoxFixture.Records(text);
        Assert.That(records.Count, Is.EqualTo(1));
        Assert.That(records[0].MessageLength, Is.EqualTo((long)"Subject: A subject\r\n\r\nThe body.\n".Length));
    }

    [Test]
    public void Scan_StopsAtTheEndOfTheSource() {
        var parser = new MBoxParser(MBoxParserOptions.Default);
        using var stream = MBoxFixture.Stream(MBoxFixture.Mailbox("The body.\n"));
        var records = new List<MBoxRecord>(parser.Scan(stream, null, CancellationToken.None));
        Assert.That(records.Count, Is.EqualTo(1));
        Assert.That(records[0].End, Is.EqualTo(stream.Length));
    }

    [Test]
    public void Scan_LeavesTheSourceOpenWhenTheIteratorIsDisposed() {
        var parser = new MBoxParser(MBoxParserOptions.Default);
        var stream = MBoxFixture.Stream(MBoxFixture.Mailbox("The body.\n"));
        using (var enumerator = parser.Scan(stream, null, CancellationToken.None).GetEnumerator()) {
            enumerator.MoveNext();
        }
        Assert.That(stream.CanRead, Is.True);
        stream.Dispose();
    }

    [Test]
    public void Scan_ReportsADiagnosticForAnEmptySource() {
        var (records, diagnostics) = MBoxFixture.Scan(string.Empty);
        Assert.That(records, Is.Empty);
        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public void Scan_RejectsAStreamThatIsNotSeekable() {
        var parser = new MBoxParser(MBoxParserOptions.Default);
        using var stream = new NonSeekableStream(new MemoryStream());
        Assert.That(
            () => parser.Scan(stream, null, CancellationToken.None).ToList(),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Scan_RejectsAStreamThatIsNotReadable() {
        var parser = new MBoxParser(MBoxParserOptions.Default);
        using var stream = new WriteOnlyStream();
        Assert.That(
            () => parser.Scan(stream, null, CancellationToken.None).ToList(),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void ReadMessage_LeavesTheSourceOpen() {
        var parser = new MBoxParser(MBoxParserOptions.Default);
        var stream = MBoxFixture.Stream(MBoxFixture.Mailbox("The body.\n"));
        var records = new List<MBoxRecord>(parser.Scan(stream, null, CancellationToken.None));
        parser.ReadMessage(stream, records[0], CancellationToken.None);
        Assert.That(stream.CanRead, Is.True);
        stream.Dispose();
    }

    [Test]
    public void ReadMessage_ReadsOnlyTheRecordThatWasRequested() {
        var text = MBoxFixture.Mailbox(["First body.\n", "Second body.\n"]);
        var parser = new MBoxParser(MBoxParserOptions.Default);
        using var stream = MBoxFixture.Stream(text);
        var records = new List<MBoxRecord>(parser.Scan(stream, null, CancellationToken.None));
        var message = parser.ReadMessage(stream, records[1], CancellationToken.None);
        Assert.That(message.Body.Text, Is.EqualTo("Second body.\n"));
    }

    [Test]
    public void ReadMessage_RejectsARecordThatDoesNotFitInTheSource() {
        var parser = new MBoxParser(MBoxParserOptions.Default);
        using var stream = MBoxFixture.Stream(MBoxFixture.Mailbox("The body.\n"));
        var records = new List<MBoxRecord>(parser.Scan(stream, null, CancellationToken.None));
        var record = records[0] with { MessageLength = 100000 };
        Assert.That(
            () => parser.ReadMessage(stream, record, CancellationToken.None),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Extract_RejectsARecordThatDoesNotFitInTheSource() {
        var parser = new MBoxParser(MBoxParserOptions.Default);
        using var stream = MBoxFixture.Stream(MBoxFixture.Mailbox("The body.\n"));
        using var destination = new MemoryStream();
        var records = new List<MBoxRecord>(parser.Scan(stream, null, CancellationToken.None));
        var record = records[0] with { MessageLength = 100000 };
        Assert.That(
            () => parser.ExtractMessage(stream, record, destination, CancellationToken.None),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Extract_LeavesTheSourceAndTheDestinationOpen() {
        var parser = new MBoxParser(MBoxParserOptions.Default);
        var stream = MBoxFixture.Stream(MBoxFixture.Mailbox("The body.\n"));
        var destination = new MemoryStream();
        var records = new List<MBoxRecord>(parser.Scan(stream, null, CancellationToken.None));
        parser.ExtractMessage(stream, records[0], destination, CancellationToken.None);
        Assert.That(stream.CanRead, Is.True);
        Assert.That(destination.CanWrite, Is.True);
        stream.Dispose();
        destination.Dispose();
    }

    /// <summary>
    /// A stream that cannot be seeked.
    /// </summary>
    private sealed class NonSeekableStream : Stream {
        private readonly Stream _Inner;

        public NonSeekableStream(Stream inner) {
            _Inner = inner;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() {
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return _Inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// A stream that cannot be read.
    /// </summary>
    private sealed class WriteOnlyStream : Stream {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() {
        }

        public override int Read(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
        }
    }
}
