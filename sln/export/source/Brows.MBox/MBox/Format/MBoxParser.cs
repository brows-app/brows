using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Brows.MBox.Format;

/// <summary>
/// Reads the records of a mailbox and the messages they contain.
/// </summary>
/// <remarks>
/// The parser holds no mutable state. Every operation is driven by the options it is given, so
/// the same parser can read a mailbox with different dialects side by side.
/// </remarks>
internal sealed class MBoxParser {
    private readonly MBoxParserOptions _Options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxParser"/> class.
    /// </summary>
    /// <param name="options">
    /// The options that control how the mailbox is read. The options are snapshotted.
    /// </param>
    internal MBoxParser(MBoxParserOptions options) {
        _Options = (options ?? throw new ArgumentNullException(nameof(options))) with { };
    }

    /// <summary>
    /// Gets the options the parser was created with.
    /// </summary>
    internal MBoxParserOptions Options =>
        _Options;

    /// <summary>
    /// Scans a mailbox and returns the records it contains.
    /// </summary>
    /// <param name="source">
    /// The mailbox to scan. The stream must be readable and seekable, and is left open.
    /// </param>
    /// <param name="diagnostic">
    /// The callback that receives the problems found while scanning. May be <see langword="null"/>.
    /// </param>
    /// <param name="token">
    /// The token used to cancel the scan.
    /// </param>
    /// <returns>
    /// The records of the mailbox, in the order they are stored.
    /// </returns>
    internal IEnumerable<MBoxRecord> Scan(Stream source,
                                        Action<MBoxDiagnostic> diagnostic,
                                        CancellationToken token) {
        ArgumentNullException.ThrowIfNull(source);
        RequireSupported(source);
        return ScanCore(source, diagnostic, token);
    }

    /// <summary>
    /// Reads the header fields of a record, without reading its body.
    /// </summary>
    /// <param name="source">
    /// The mailbox to read from. The stream must be readable and seekable, and is left open.
    /// </param>
    /// <param name="record">
    /// The record to read.
    /// </param>
    /// <param name="token">
    /// The token used to cancel the read.
    /// </param>
    /// <returns>
    /// The message of the record, with a body that was not read.
    /// </returns>
    internal MBoxMessage ReadHeaders(Stream source, MBoxRecord record, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(record);
        RequireSupported(source);
        var reader = CreateReader(source, record);
        var diagnostics = new List<MBoxDiagnostic>();
        reader.Seek(record.MessageOffset, token);
        var headerRead = ReadHeaderBlock(
            reader,
            _Options.MaxHeaderBytes,
            _Options.MaxHeaderCount,
            diagnostics,
            token);
        var body = new MBoxBody(
            contentType: MBoxContentTypes.TextPlain,
            parts: Array.Empty<MBoxPart>(),
            text: null,
            html: null,
            state: MBoxBodyState.NotRead,
            length: null,
            diagnostics: Array.Empty<MBoxDiagnostic>());
        return new MBoxMessage(record, headerRead.Headers, body, diagnostics);
    }

    /// <summary>
    /// Reads a record, including a bounded preview of its body.
    /// </summary>
    /// <param name="source">
    /// The mailbox to read from. The stream must be readable and seekable, and is left open.
    /// </param>
    /// <param name="record">
    /// The record to read.
    /// </param>
    /// <param name="token">
    /// The token used to cancel the read.
    /// </param>
    /// <returns>
    /// The message of the record.
    /// </returns>
    internal MBoxMessage ReadMessage(Stream source, MBoxRecord record, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(record);
        RequireSupported(source);
        var reader = CreateReader(source, record);
        var diagnostics = new List<MBoxDiagnostic>();
        reader.Seek(record.MessageOffset, token);
        var headerRead = ReadHeaderBlock(
            reader,
            _Options.MaxHeaderBytes,
            _Options.MaxHeaderCount,
            diagnostics,
            token);
        MBoxBody body;
        if (headerRead.IsComplete) {
            body = MBoxBodyReader.Read(reader, record, headerRead.Headers, _Options, diagnostics, token);
        }
        else {
            var contentType = MBoxContentTypeParser.Parse(
                headerRead.Headers.Get("Content-Type")?.RawValue,
                diagnostics,
                record.MessageOffset);
            long? bodyLength = record.BodyOffset.HasValue
                ? record.MessageOffset + record.MessageLength - record.BodyOffset.Value
                : null;
            body = new MBoxBody(
                contentType,
                Array.Empty<MBoxPart>(),
                null,
                null,
                MBoxBodyState.Truncated,
                bodyLength,
                diagnostics);
        }
        return new MBoxMessage(record, headerRead.Headers, body, diagnostics);
    }

    /// <summary>
    /// Extracts a record as an Internet message, without altering its bytes.
    /// </summary>
    /// <param name="source">
    /// The mailbox to read from. The stream must be readable and seekable, and is left open.
    /// </param>
    /// <param name="record">
    /// The record to extract.
    /// </param>
    /// <param name="destination">
    /// The stream that receives the message. The stream is left open.
    /// </param>
    /// <param name="token">
    /// The token used to cancel the extraction.
    /// </param>
    internal void ExtractMessage(Stream source,
                               MBoxRecord record,
                               Stream destination,
                               CancellationToken token) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(destination);
        RequireSupported(source);
        MBoxExtractor.Extract(source, record, destination, _Options, token);
    }

    /*
     * Reads the attachments of a record, and passes each of them to the callback as soon as it
     * is decoded. The body is read like a preview, but the input limit of a preview does not
     * apply, so that an attachment is read to its end. Returns false when a limit stopped the
     * read, so that attachments of the record may not have been read.
     */
    internal bool ExtractAttachments(Stream source,
                                     MBoxRecord record,
                                     Action<MBoxAttachment> attachment,
                                     CancellationToken token) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(attachment);
        RequireSupported(source);
        var options = _Options with {
            MaxPreviewInputBytes = MBoxParserOptions.PreviewInputBytesLimit,
        };
        var reader = CreateReader(source, record);
        var diagnostics = new List<MBoxDiagnostic>();
        reader.Seek(record.MessageOffset, token);
        var headerRead = ReadHeaderBlock(
            reader,
            options.MaxHeaderBytes,
            options.MaxHeaderCount,
            diagnostics,
            token);
        if (headerRead.IsComplete == false) {
            /*
             * A record that ends within its header block has no attachments.
             */
            return headerRead.LimitReached == false;
        }
        return MBoxBodyReader.ReadAttachments(
            reader,
            record,
            headerRead.Headers,
            options,
            diagnostics,
            attachment,
            token);
    }

    private IEnumerable<MBoxRecord> ScanCore(Stream source,
                                             Action<MBoxDiagnostic> diagnostic,
                                             CancellationToken token) {
        var reader = new MBoxLineReader(source, _Options.BufferSize);
        var sink = new MBoxDiagnosticSink(diagnostic);
        var framing = new RecordFraming();
        /*
         * True when the previous physical line was empty, so the current line may start a
         * separator. A separator is only recognized at a physical line start that follows an
         * actually empty line, or at the start of the source.
         */
        var afterEmptyLine = false;
        var accepted = 0;
        MBoxRecord pending = null;
        while (true) {
            token.ThrowIfCancellationRequested();
            if (reader.TryReadLine(_Options.MaxEnvelopeBytes, out var line, token) == false) {
                break;
            }
            if (line.Length == 0) {
                afterEmptyLine = true;
                framing.Note(line);
                continue;
            }
            var isCandidate = line.Offset == 0 || afterEmptyLine;
            afterEmptyLine = false;
            if (isCandidate == false) {
                framing.Note(line);
                continue;
            }
            var envelopeDiagnostics = new List<MBoxDiagnostic>();
            var envelopeBytes = reader.Line.AsSpan(0, line.Kept).ToArray();
            var envelopeRaw = Encoding.Latin1.GetString(envelopeBytes);
            var sender = MBoxEnvelopeReader.Read(
                envelopeBytes,
                line.Kept,
                out var date,
                envelopeDiagnostics,
                line.Offset);
            if (sender is null) {
                if (TryRecover(
                        reader,
                        line,
                        envelopeBytes,
                        envelopeDiagnostics,
                        token,
                        out var recoveredSender) == false) {
                    sink.AddRange(envelopeDiagnostics);
                    if (line.Offset == 0) {
                        ReportInvalidPrefix(sink);
                        yield break;
                    }
                    /*
                     * The candidate is not a separator, so its bytes remain body data.
                     */
                    framing.Note(line);
                    continue;
                }
                sender = recoveredSender;
                date = null;
            }
            if (accepted == 0 && line.Offset != 0) {
                /*
                 * A mailbox that does not begin with an accepted envelope at offset zero is not
                 * a mailbox, and its prefix is never skipped silently.
                 */
                sink.AddRange(envelopeDiagnostics);
                ReportInvalidPrefix(sink);
                yield break;
            }
            afterEmptyLine = false;
            accepted++;
            sink.AddRange(envelopeDiagnostics);
            var record = new MBoxRecord(
                offset: line.Offset,
                length: 0,
                messageOffset: line.Offset + line.Length + line.TerminatorLength,
                messageLength: 0,
                bodyOffset: null,
                format: _Options.Format,
                envelope: new MBoxEnvelope(
                    sender,
                    envelopeRaw,
                    date,
                    envelopeDiagnostics),
                diagnostics: envelopeDiagnostics);
            if (pending is not null) {
                yield return Finalize(pending, record.Offset, framing);
            }
            pending = record;
            framing = new RecordFraming();
            framing.Begin(record.MessageOffset);
        }
        if (pending is not null) {
            yield return Finalize(pending, source.Length, framing);
        }
        if (accepted == 0 && source.Length > 0) {
            ReportInvalidPrefix(sink);
        }
        sink.Summarize();
    }

    /// <summary>
    /// Completes a record with the extents that were found after it was accepted.
    /// </summary>
    /// <param name="record">
    /// The record to complete.
    /// </param>
    /// <param name="end">
    /// The absolute offset at which the record ends.
    /// </param>
    /// <param name="framing">
    /// The framing that was observed while the record was scanned.
    /// </param>
    /// <returns>
    /// The completed record.
    /// </returns>
    private MBoxRecord Finalize(MBoxRecord record, long end, RecordFraming framing) {
        var length = end - record.Offset;
        var messageLength = length - (record.MessageOffset - record.Offset);
        /*
         * The empty line that separates the record from the next one is framing, not part of
         * the message.
         */
        messageLength -= framing.FramingLength();
        return record with {
            Length = length,
            MessageLength = messageLength,
            BodyOffset = framing.BodyOffset(_Options.MaxHeaderBytes),
        };
    }

    private static void ReportInvalidPrefix(MBoxDiagnosticSink sink) {
        sink.Add(new MBoxDiagnostic(
            MBoxDiagnosticCode.MalformedEnvelope,
            0,
            "The source does not begin with an accepted envelope line."));
    }

    private bool TryRecover(MBoxLineReader reader,
                            MBoxLine line,
                            byte[] lineBytes,
                            List<MBoxDiagnostic> diagnostics,
                            CancellationToken token,
                            out string sender) {
        sender = null;
        if (line.Kept < 5) {
            return false;
        }
        if (lineBytes[0] != (byte)'F' ||
            lineBytes[1] != (byte)'r' ||
            lineBytes[2] != (byte)'o' ||
            lineBytes[3] != (byte)'m' ||
            lineBytes[4] != (byte)' ') {
            /*
             * The line is ordinary text, not a candidate envelope.
             */
            return false;
        }
        var headers = new List<KeyValuePair<string, string>>();
        var sawEmpty = false;
        var save = reader.Position;
        var bytes = 0;
        while (true) {
            var hasByteBudget = bytes < _Options.MaxHeaderBytes;
            var hasFieldBudget = headers.Count < _Options.MaxHeaderCount;
            var canReadHeader = hasByteBudget && hasFieldBudget;
            if (canReadHeader == false) {
                break;
            }
            token.ThrowIfCancellationRequested();
            var remaining = _Options.MaxHeaderBytes - bytes;
            if (reader.TryReadLine(remaining, remaining, out var headerLine, token) == false) {
                break;
            }
            bytes += headerLine.Length + headerLine.TerminatorLength;
            if (headerLine.IsComplete == false) {
                break;
            }
            if (headerLine.Length == 0) {
                sawEmpty = true;
                break;
            }
            var headerBytes = reader.Line;
            var colon = Array.IndexOf(headerBytes, (byte)':', 0, headerLine.Kept);
            if (colon < 0) {
                continue;
            }
            var name = Encoding.ASCII.GetString(headerBytes, 0, colon).Trim();
            var value = Encoding.UTF8.GetString(headerBytes, colon + 1, headerLine.Kept - colon - 1).Trim();
            headers.Add(new KeyValuePair<string, string>(name, value));
        }
        reader.Seek(save, token);
        var recoveredHeaderBlock = MBoxEnvelopeReader.IsRecoverable(headers);
        var cannotRecover = sawEmpty == false || recoveredHeaderBlock == false;
        if (cannotRecover) {
            diagnostics.Add(new MBoxDiagnostic(
                MBoxDiagnosticCode.AmbiguousRecovery,
                line.Offset,
                "The candidate separator could not be confirmed and was kept as body data."));
            return false;
        }
        sender = MBoxEnvelopeReader.RecoverSender(lineBytes, line.Kept);
        diagnostics.Add(new MBoxDiagnostic(
            MBoxDiagnosticCode.RecoveredRecord,
            line.Offset,
            "The record was recovered using bounded header lookahead."));
        return true;
    }

    private static void RequireSupported(Stream source) {
        if (source.CanRead == false) {
            throw new ArgumentException("The stream must be readable.", nameof(source));
        }
        if (source.CanSeek == false) {
            throw new ArgumentException("The stream must be seekable.", nameof(source));
        }
    }

    private MBoxLineReader CreateReader(Stream source, MBoxRecord record) {
        var start = record.MessageOffset;
        var end = checked(start + record.MessageLength);
        if (start < 0 || end < start || end > source.Length) {
            throw new ArgumentOutOfRangeException(
                nameof(record),
                "The record does not fit in the source.");
        }
        var reader = new MBoxLineReader(source, _Options.BufferSize) {
            Limit = end,
        };
        return reader;
    }

    internal static MBoxHeaderReadResult ReadHeaderBlock(MBoxLineReader reader,
                                                         int maxBytes,
                                                         int maxCount,
                                                         List<MBoxDiagnostic> diagnostics,
                                                         CancellationToken token) {
        var fields = new List<MBoxHeader>();
        var bytes = 0;
        var ended = false;
        var limitReached = false;
        string pendingName = null;
        StringBuilder pendingValue = null;
        long pendingOffset = 0;
        while (bytes < maxBytes) {
            token.ThrowIfCancellationRequested();
            var remaining = maxBytes - bytes;
            if (reader.TryReadLine(remaining, remaining, out var line, token) == false) {
                break;
            }
            bytes += line.Length + line.TerminatorLength;
            var lineBytes = reader.Line;
            var kept = line.Kept;
            if (line.Length == 0) {
                AddPendingHeader(fields, pendingName, pendingValue, pendingOffset, diagnostics);
                pendingName = null;
                pendingValue = null;
                ended = true;
                break;
            }
            var hasContinuationPrefix = kept > 0 && (lineBytes[0] == (byte)' ' || lineBytes[0] == (byte)'\t');
            var hasPendingHeader = pendingName is not null;
            var isFoldedHeader = hasContinuationPrefix && hasPendingHeader;
            if (isFoldedHeader) {
                pendingValue.Append(' ');
                pendingValue.Append(Encoding.UTF8.GetString(lineBytes, 0, kept).Trim());
                if (line.IsComplete == false) {
                    limitReached = true;
                    AddHeaderLimitDiagnostic(diagnostics, line.Offset);
                    AddPendingHeader(fields, pendingName, pendingValue, pendingOffset, diagnostics);
                    pendingName = null;
                    pendingValue = null;
                    break;
                }
                continue;
            }
            AddPendingHeader(fields, pendingName, pendingValue, pendingOffset, diagnostics);
            pendingName = null;
            pendingValue = null;
            if (fields.Count >= maxCount) {
                diagnostics.Add(new MBoxDiagnostic(
                    MBoxDiagnosticCode.HeaderLimitReached,
                    line.Offset,
                    "The header block contains more fields than the configured limit allows."));
                limitReached = true;
                break;
            }
            var colon = Array.IndexOf(lineBytes, (byte)':', 0, kept);
            if (colon < 0) {
                var name = Encoding.ASCII.GetString(lineBytes, 0, kept).Trim();
                diagnostics.Add(new MBoxDiagnostic(
                    MBoxDiagnosticCode.MalformedHeader,
                    line.Offset,
                    "The header field has no colon and was kept with an empty value."));
                fields.Add(new MBoxHeader(
                    name,
                    string.Empty,
                    DecodeValue(name, string.Empty, diagnostics, line.Offset),
                    fields.Count));
            }
            else {
                pendingName = Encoding.ASCII.GetString(lineBytes, 0, colon).Trim();
                pendingValue = new StringBuilder(Encoding.UTF8.GetString(lineBytes, colon + 1, kept - colon - 1));
                pendingOffset = line.Offset;
            }
            if (line.IsComplete == false) {
                limitReached = true;
                AddHeaderLimitDiagnostic(diagnostics, line.Offset);
                AddPendingHeader(fields, pendingName, pendingValue, pendingOffset, diagnostics);
                pendingName = null;
                pendingValue = null;
                break;
            }
        }
        AddPendingHeader(fields, pendingName, pendingValue, pendingOffset, diagnostics);
        var headerDidNotEndWithinBudget = ended == false && limitReached == false;
        if (headerDidNotEndWithinBudget) {
            if (bytes >= maxBytes) {
                limitReached = true;
                AddHeaderLimitDiagnostic(diagnostics, reader.Position);
            }
            else {
                diagnostics.Add(new MBoxDiagnostic(
                    MBoxDiagnosticCode.MalformedHeader,
                    reader.Position,
                    "The header block ends without an empty line."));
            }
        }
        return new MBoxHeaderReadResult(new MBoxHeaders(fields), bytes, ended, limitReached);
    }

    private static void AddPendingHeader(List<MBoxHeader> fields,
                                         string name,
                                         StringBuilder value,
                                         long offset,
                                         List<MBoxDiagnostic> diagnostics) {
        if (name is null) {
            return;
        }
        var rawValue = value.ToString().Trim();
        fields.Add(new MBoxHeader(name, rawValue, DecodeValue(name, rawValue, diagnostics, offset), fields.Count));
    }

    private static void AddHeaderLimitDiagnostic(List<MBoxDiagnostic> diagnostics, long offset) {
        diagnostics.Add(new MBoxDiagnostic(
            MBoxDiagnosticCode.HeaderLimitReached,
            offset,
            "The header block is longer than the configured limit allows."));
    }

    private static string DecodeValue(string name,
                                      string value,
                                      List<MBoxDiagnostic> diagnostics,
                                      long offset) {
        if (value.Length == 0) {
            return value;
        }
        if (string.Equals(name, "From", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "To", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Cc", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Bcc", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Reply-To", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Sender", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Resent-From", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Resent-To", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Resent-Cc", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Resent-Bcc", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Resent-Reply-To", StringComparison.OrdinalIgnoreCase)) {
            return MBoxEncodedWord.DecodeAddress(value, diagnostics, offset);
        }
        if (string.Equals(name, "Message-ID", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Content-Disposition", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Received", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("X-", StringComparison.OrdinalIgnoreCase)) {
            return value;
        }
        if (string.Equals(name, "Date", StringComparison.OrdinalIgnoreCase)) {
            var dateWasParsed = MBoxDate.TryParseHeader(value, out _, out var invalidValue);
            var hasRecognizedInvalidDate = dateWasParsed == false && invalidValue;
            if (hasRecognizedInvalidDate) {
                diagnostics.Add(new MBoxDiagnostic(
                    MBoxDiagnosticCode.MalformedDate,
                    offset,
                    "The message date has a recognized syntax but an invalid value."));
            }
            return value;
        }
        return MBoxEncodedWord.DecodeUnstructured(value, diagnostics, offset);
    }

    /// <summary>
    /// Tracks the framing of a stored record while the mailbox is scanned.
    /// </summary>
    private sealed class RecordFraming {
        private long _MessageOffset;
        private long _HeaderEnd = -1;
        private long _HeaderEndAfter = -1;
        private long _TrailingEmpty = -1;
        private int _TrailingEmptyTerminator;

        /// <summary>
        /// Starts the tracking of a record.
        /// </summary>
        /// <param name="messageOffset">
        /// The absolute offset of the first message byte of the record.
        /// </param>
        internal void Begin(long messageOffset) {
            _MessageOffset = messageOffset;
        }

        /// <summary>
        /// Notes a line of the record.
        /// </summary>
        /// <param name="line">
        /// The line.
        /// </param>
        internal void Note(MBoxLine line) {
            if (line.Length != 0) {
                /*
                 * The empty line that frames the record must be the last line of the record.
                 */
                _TrailingEmpty = -1;
                return;
            }
            if (_HeaderEnd < 0) {
                /*
                 * The first empty line of the record terminates its header block.
                 */
                _HeaderEnd = line.Offset;
                _HeaderEndAfter = line.Offset + line.TerminatorLength;
            }
            _TrailingEmpty = line.Offset;
            _TrailingEmptyTerminator = line.TerminatorLength;
        }

        /// <summary>
        /// Returns the absolute offset of the first body byte of the record.
        /// </summary>
        /// <param name="maxHeaderBytes">
        /// The maximum number of bytes the header block may use.
        /// </param>
        /// <returns>
        /// The absolute offset of the first body byte, or <see langword="null"/> when the end of
        /// the header block cannot be found within the limit.
        /// </returns>
        internal long? BodyOffset(int maxHeaderBytes) {
            if (_HeaderEnd < 0) {
                return null;
            }
            if (_HeaderEndAfter - _MessageOffset > maxHeaderBytes) {
                return null;
            }
            return _HeaderEndAfter;
        }

        /// <summary>
        /// Returns the number of bytes of the empty line that separates the record from the next
        /// one.
        /// </summary>
        /// <returns>
        /// The number of bytes of the framing empty line, or zero when the record does not end
        /// with one.
        /// </returns>
        internal int FramingLength() {
            if (_TrailingEmpty < 0) {
                return 0;
            }
            if (_TrailingEmpty == _HeaderEnd) {
                /*
                 * The empty line terminates the header block of the record, so it is not the
                 * framing that separates the record from the next one.
                 */
                return 0;
            }
            return _TrailingEmptyTerminator;
        }
    }
}
