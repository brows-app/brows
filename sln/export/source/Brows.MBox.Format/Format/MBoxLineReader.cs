using System;
using System.IO;
using System.Threading;

namespace Brows.MBox.Format;

/// <summary>
/// Reads a stream one line at a time, keeping at most a bounded prefix of each line.
/// </summary>
/// <remarks>
/// The reader never buffers more than one line, so a mailbox with very long lines is read without
/// holding the whole line in memory. The bytes that are kept are the first ones of the line, and
/// the whole line can be copied to a sink while it is read.
/// </remarks>
internal sealed class MBoxLineReader {
    private readonly Stream Stream;
    private readonly byte[] Buffer;
    private readonly int BufferSize;
    private int BufferStart;
    private int BufferEnd;
    private long BufferOrigin;
    private bool ReachedEnd;
    private byte[] _Line;

    /// <summary>
    /// Gets the absolute offset, from the start of the stream, of the next byte to be read.
    /// </summary>
    public long Position { get; private set; }

    /// <summary>
    /// Gets or sets the absolute offset, from the start of the stream, at which the reader stops.
    /// </summary>
    public long Limit { get; set; } =
        long.MaxValue;

    /// <summary>
    /// Gets the bytes of the line that was read last, up to the limit the reader was given.
    /// </summary>
    public byte[] Line =>
        _Line;

    /// <summary>
    /// Gets the number of bytes of the line that was read last that are kept in <see cref="Line"/>.
    /// </summary>
    public int LineLength { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxLineReader"/> class.
    /// </summary>
    /// <param name="stream">
    /// The stream to read from.
    /// </param>
    /// <param name="bufferSize">
    /// The size of the read buffer.
    /// </param>
    public MBoxLineReader(Stream stream, int bufferSize) {
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        BufferSize = bufferSize < 1024 ? 1024 : bufferSize;
        Buffer = new byte[BufferSize];
        _Line = new byte[1024];
    }

    /// <summary>
    /// Reads the next line, keeping a bounded prefix of it.
    /// </summary>
    /// <param name="maxBytes">
    /// The maximum number of bytes of the line to keep. The line is always read to its end.
    /// </param>
    /// <param name="line">
    /// The line that was read.
    /// </param>
    /// <param name="token">
    /// The token used to cancel the read.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a line was read.
    /// </returns>
    public bool TryReadLine(int maxBytes, out MBoxLine line, CancellationToken token) {
        return TryReadLine(maxBytes, null, long.MaxValue, out line, token);
    }

    public bool TryReadLine(int maxKeepBytes,
                            int maxReadBytes,
                            out MBoxLine line,
                            CancellationToken token) {
        if (maxReadBytes <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(maxReadBytes),
                maxReadBytes,
                "The read limit must be positive.");
        }
        return TryReadLine(maxKeepBytes, null, maxReadBytes, out line, token);
    }

    /// <summary>
    /// Reads the next line, keeping a bounded prefix of it and copying its content to a sink.
    /// </summary>
    /// <param name="maxBytes">
    /// The maximum number of bytes of the line to keep. The line is always read to its end.
    /// </param>
    /// <param name="sink">
    /// The stream that receives the content of the line, without its terminator. May be
    /// <see langword="null"/>.
    /// </param>
    /// <param name="line">
    /// The line that was read.
    /// </param>
    /// <param name="token">
    /// The token used to cancel the read.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a line was read.
    /// </returns>
    public bool TryReadLine(int maxBytes, IMBoxLineSink sink, out MBoxLine line, CancellationToken token) {
        return TryReadLine(maxBytes, sink, long.MaxValue, out line, token);
    }

    private bool TryReadLine(int maxBytes,
                             IMBoxLineSink sink,
                             long maxReadBytes,
                             out MBoxLine line,
                             CancellationToken token) {
        line = default;
        var start = Position;
        var rawLength = 0;
        var kept = 0;
        var found = false;
        var reachedLimit = false;
        var reachedReadLimit = false;
        /*
         * The last byte of the line is held back until it is known whether it belongs to the
         * terminator of the line or to its content.
         */
        var pending = -1;
        while (true) {
            token.ThrowIfCancellationRequested();
            if (Position >= Limit) {
                reachedLimit = true;
                break;
            }
            if (BufferStart == BufferEnd) {
                if (ReachedEnd) {
                    break;
                }
                var read = Stream.Read(Buffer, 0, BufferSize);
                if (read <= 0) {
                    ReachedEnd = true;
                    break;
                }
                BufferStart = 0;
                BufferEnd = read;
                BufferOrigin = Position;
            }
            var available = BufferEnd - BufferStart;
            var span = Buffer.AsSpan(BufferStart, available);
            var room = Limit - Position;
            if (room < available) {
                span = span[..(int)room];
                reachedLimit = true;
            }
            var readRoom = maxReadBytes - (Position - start);
            if (readRoom < span.Length) {
                span = span[..(int)readRoom];
                reachedReadLimit = true;
            }
            var index = span.IndexOf((byte)'\n');
            if (index < 0) {
                rawLength += span.Length;
                kept = Keep(span, kept, maxBytes);
                Write(sink, span, ref pending);
                BufferStart += span.Length;
                Position += span.Length;
                var readStoppedAtLimit = reachedLimit || reachedReadLimit;
                if (readStoppedAtLimit) {
                    break;
                }
                continue;
            }
            var content = span[..index];
            rawLength += content.Length;
            kept = Keep(content, kept, maxBytes);
            Write(sink, content, ref pending);
            BufferStart += index + 1;
            Position += index + 1;
            found = true;
            break;
        }
        var noLineWasRead = found == false && rawLength == 0;
        if (noLineWasRead) {
            return false;
        }
        var isCrlf = found && pending == (byte)'\r';
        var length = isCrlf ? rawLength - 1 : rawLength;
        var terminatorLength = found == false ? 0 : isCrlf ? 2 : 1;
        if (isCrlf == false && pending >= 0) {
            /*
             * The held byte belongs to the content of the line.
             */
            sink?.WriteByte((byte)pending);
        }
        kept = Math.Min(kept, length);
        LineLength = kept;
        var lineIsComplete = found || reachedLimit || ReachedEnd;
        var cappedLineEndsWithCarriageReturn = found == false && reachedReadLimit && pending == (byte)'\r';
        line = new MBoxLine {
            Offset = start,
            Length = length,
            Kept = kept,
            TerminatorLength = terminatorLength,
            IsComplete = lineIsComplete,
            EndsWithCarriageReturn = cappedLineEndsWithCarriageReturn,
        };
        sink?.EndLine(terminatorLength);
        return true;
    }

    /// <summary>
    /// Writes the bytes of the line to the sink, holding back its last byte.
    /// </summary>
    /// <param name="sink">
    /// The sink that receives the bytes.
    /// </param>
    /// <param name="span">
    /// The bytes to write.
    /// </param>
    /// <param name="pending">
    /// The byte that is held back.
    /// </param>
    private static void Write(IMBoxLineSink sink, ReadOnlySpan<byte> span, ref int pending) {
        if (span.Length == 0) {
            return;
        }
        if (pending >= 0) {
            sink?.WriteByte((byte)pending);
        }
        if (span.Length > 1) {
            sink?.Write(span[..^1]);
        }
        pending = span[^1];
    }

    private int Keep(ReadOnlySpan<byte> span, int kept, int maxBytes) {
        var room = maxBytes - kept;
        if (room <= 0) {
            return kept;
        }
        var count = Math.Min(room, span.Length);
        if (kept + count > _Line.Length) {
            var size = Math.Max(_Line.Length * 2, kept + count);
            Array.Resize(ref _Line, size);
        }
        span[..count].CopyTo(_Line.AsSpan(kept));
        return kept + count;
    }

    /// <summary>
    /// Moves the reader to the specified absolute offset.
    /// </summary>
    /// <param name="offset">
    /// The absolute offset to move to.
    /// </param>
    /// <param name="token">
    /// The token used to cancel the operation.
    /// </param>
    public void Seek(long offset, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        if (offset == Position) {
            return;
        }
        var bufferOffset = BufferOrigin + BufferStart;
        var bufferEndOffset = BufferOrigin + BufferEnd;
        if (offset >= bufferOffset && offset <= bufferEndOffset) {
            BufferStart += (int)(offset - bufferOffset);
            Position = offset;
            return;
        }
        Stream.Seek(offset, SeekOrigin.Begin);
        Position = offset;
        BufferStart = 0;
        BufferEnd = 0;
        BufferOrigin = offset;
        ReachedEnd = false;
    }
}
