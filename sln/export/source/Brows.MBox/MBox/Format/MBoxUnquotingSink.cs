using System;
using System.IO;

namespace Brows.MBox.Format;

/// <summary>
/// Writes the lines of a stored body, removing the quoting that the mailbox applies to them.
/// </summary>
/// <remarks>
/// The sink removes exactly one quote mark from the lines that the dialect quotes, and copies
/// every other byte as it is. The terminator of a line is always the terminator of the source,
/// so the extracted message keeps the line endings of the mailbox.
/// <para/>
/// The decision is made while the line is read, so the sink never holds a whole line in memory.
/// A line is quoted when it starts with one or more quote marks that are followed by
/// <c>From </c>. The mboxrd dialect accepts any number of quote marks, while the mboxo dialect
/// accepts exactly one.
/// </remarks>
internal sealed class MBoxUnquotingSink : IMBoxLineSink {
    /// <summary>
    /// The number of bytes of the <c>From </c> prefix that a quoted line follows with.
    /// </summary>
    private const int FromLength = 5;

    private readonly Stream _Destination;
    private readonly MBoxFormat _Format;
    private readonly byte[] _Candidate = new byte[FromLength];
    private State _State;
    private int _RunLength;
    private int _CandidateLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxUnquotingSink"/> class.
    /// </summary>
    /// <param name="destination">
    /// The stream that receives the lines.
    /// </param>
    /// <param name="format">
    /// The mailbox storage dialect of the body.
    /// </param>
    internal MBoxUnquotingSink(Stream destination, MBoxFormat format) {
        _Destination = destination ?? throw new ArgumentNullException(nameof(destination));
        _Format = format;
    }

    /// <summary>
    /// Appends the specified bytes to the line that is being read.
    /// </summary>
    /// <param name="bytes">
    /// The bytes to append.
    /// </param>
    public void Write(ReadOnlySpan<byte> bytes) {
        if (bytes.Length == 0) {
            return;
        }
        if (_State == State.Done) {
            _Destination.Write(bytes);
            return;
        }
        var index = 0;
        while (index < bytes.Length) {
            if (_State == State.Done) {
                _Destination.Write(bytes[index..]);
                return;
            }
            WriteByte(bytes[index]);
            index++;
        }
    }

    /// <summary>
    /// Appends the specified byte to the line that is being read.
    /// </summary>
    /// <param name="value">
    /// The byte to append.
    /// </param>
    public void WriteByte(byte value) {
        switch (_State) {
            case State.Start:
                if (value != (byte)'>') {
                    /*
                     * A line that does not start with a quote mark is never quoted.
                     */
                    _Destination.WriteByte(value);
                    _State = State.Done;
                    return;
                }
                _RunLength = 1;
                _State = State.Run;
                return;
            case State.Run:
                if (value == (byte)'>') {
                    _RunLength++;
                    return;
                }
                _Candidate[0] = value;
                _CandidateLength = 1;
                _State = State.Candidate;
                return;
            case State.Candidate:
                _Candidate[_CandidateLength++] = value;
                if (_CandidateLength == FromLength) {
                    Decide();
                }
                return;
            default:
                _Destination.WriteByte(value);
                return;
        }
    }

    /// <summary>
    /// Ends the line that was read, and writes it with the quoting removed.
    /// </summary>
    /// <param name="terminatorLength">
    /// The number of bytes of the terminator of the line.
    /// </param>
    public void EndLine(int terminatorLength) {
        if (_State == State.Run || _State == State.Candidate) {
            /*
             * The line ended before the "From " prefix was complete, so it is not quoted.
             */
            WriteRun(false);
            WriteCandidate();
        }
        _State = State.Start;
        _RunLength = 0;
        _CandidateLength = 0;
        if (terminatorLength == 2) {
            _Destination.WriteByte((byte)'\r');
            _Destination.WriteByte((byte)'\n');
        }
        else if (terminatorLength == 1) {
            _Destination.WriteByte((byte)'\n');
        }
    }

    /// <summary>
    /// Decides whether the line is quoted, and writes the part of it that was held back.
    /// </summary>
    private void Decide() {
        var followsWithFrom = true;
        for (var i = 0; i < FromLength; i++) {
            if (_Candidate[i] != FromPrefix[i]) {
                followsWithFrom = false;
                break;
            }
        }
        var quoted = MBoxUnquote.IsQuoted(_RunLength, followsWithFrom, _Format);
        WriteRun(quoted);
        WriteCandidate();
        _State = State.Done;
    }

    /// <summary>
    /// Writes the quote marks of the line that were held back.
    /// </summary>
    /// <param name="quoted">
    /// Whether the line is quoted, which removes the first quote mark of the line.
    /// </param>
    private void WriteRun(bool quoted) {
        var count = quoted ? _RunLength - 1 : _RunLength;
        for (var i = 0; i < count; i++) {
            _Destination.WriteByte((byte)'>');
        }
    }

    /// <summary>
    /// Writes the bytes that follow the quote marks of the line.
    /// </summary>
    private void WriteCandidate() {
        if (_CandidateLength > 0) {
            _Destination.Write(_Candidate, 0, _CandidateLength);
        }
    }

    /// <summary>
    /// The prefix that a quoted line follows its quote marks with.
    /// </summary>
    private static ReadOnlySpan<byte> FromPrefix =>
        "From "u8;

    /// <summary>
    /// The states of the sink while a line is read.
    /// </summary>
    private enum State {
        /// <summary>
        /// The first byte of the line was not read yet.
        /// </summary>
        Start,

        /// <summary>
        /// The quote marks of the line are being read.
        /// </summary>
        Run,

        /// <summary>
        /// The bytes that follow the quote marks of the line are being read.
        /// </summary>
        Candidate,

        /// <summary>
        /// The quoting of the line is known, so the rest of the line is copied as it is.
        /// </summary>
        Done,
    }
}
