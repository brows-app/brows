using System;
using System.Collections.Generic;
using System.Threading;

namespace Brows.MBox.Format;

/// <summary>
/// Reads the body of a message and selects the text to display.
/// </summary>
internal static class MBoxBodyReader {
    /// <summary>
    /// Reads the body of a record and selects the text to display.
    /// </summary>
    /// <param name="reader">
    /// The reader positioned at the start of the body.
    /// </param>
    /// <param name="record">
    /// The record being read.
    /// </param>
    /// <param name="headers">
    /// The header fields of the record.
    /// </param>
    /// <param name="options">
    /// The options that control how the body is read.
    /// </param>
    /// <param name="diagnostics">
    /// The list that receives the problems found while reading.
    /// </param>
    /// <param name="token">
    /// The token used to cancel the read.
    /// </param>
    /// <returns>
    /// The body of the record.
    /// </returns>
    internal static MBoxBody Read(MBoxLineReader reader,
                                MBoxRecord record,
                                MBoxHeaders headers,
                                MBoxParserOptions options,
                                List<MBoxDiagnostic> diagnostics,
                                CancellationToken token) {
        var contentTypeDiagnosticStart = diagnostics.Count;
        var contentType = MBoxContentTypeParser.Parse(
            headers.Get("Content-Type")?.RawValue,
            diagnostics,
            record.MessageOffset);
        var contentTypeDiagnosticEnd = diagnostics.Count;
        var entity = MBoxMimeEntity.Create(contentType, headers, null);
        var state = new ReadState {
            Reader = reader,
            Record = record,
            Options = options,
            Diagnostics = diagnostics,
            Token = token,
        };
        var parts = new List<MBoxPart>();
        var selection = MBoxSelection.Empty;
        var isMultipart = entity.ContentType.IsMultipart &&
                          string.IsNullOrEmpty(entity.ContentType.Boundary) == false;
        if (isMultipart) {
            selection = MBoxMimeReader.ReadMultipart(state, entity, parts, 0);
        }
        else if (entity.ContentType.IsMultipart) {
            diagnostics.Add(new MBoxDiagnostic(
                MBoxDiagnosticCode.MalformedMime,
                record.MessageOffset,
                "The multipart body has no boundary parameter."));
            selection = new MBoxSelection { State = MBoxBodyState.Malformed };
        }
        else {
            if (state.TryAddPart()) {
                var part = ReadLeaf(state, entity, parts, 0);
                selection = MBoxSelection.Of(part);
            }
        }
        for (var i = contentTypeDiagnosticStart; i < contentTypeDiagnosticEnd; i++) {
            if (diagnostics[i].Code == MBoxDiagnosticCode.MalformedMime) {
                selection = MBoxMimeReader.MarkMalformed(selection);
                break;
            }
        }
        long? bodyLength = record.BodyOffset.HasValue
            ? record.MessageOffset + record.MessageLength - record.BodyOffset.Value
            : null;
        var bodyState = selection.State;
        var bodyStateCanBeTruncated = bodyState != MBoxBodyState.Malformed &&
                                      bodyState != MBoxBodyState.NotRead;
        var limitTruncatesBody = state.LimitReached && bodyStateCanBeTruncated;
        if (limitTruncatesBody) {
            /*
             * A limit cut a retained part of the body short, so the result is not a complete
             * preview.
             */
            bodyState = MBoxBodyState.Truncated;
        }
        if (state.InputLimitReached) {
            diagnostics.Add(new MBoxDiagnostic(
                MBoxDiagnosticCode.LimitReached,
                record.MessageOffset,
                "The body is longer than the configured preview input limit allows."));
        }
        return new MBoxBody(
            contentType: contentType,
            parts: parts,
            text: selection.Text,
            html: selection.Html,
            state: bodyState,
            length: bodyLength,
            diagnostics: diagnostics);
    }

    internal static MBoxPart ReadLeaf(ReadState state,
                                      MBoxMimeEntity entity,
                                      List<MBoxPart> parts,
                                      int depth) {
        var buffer = new MBoxByteBuffer();
        var wasTruncated = false;
        while (true) {
            state.Token.ThrowIfCancellationRequested();
            if (state.StopReading) {
                break;
            }
            if (state.TryReadLine(out var line, out var lineBytes) == false) {
                wasTruncated = state.InputLimitReached;
                break;
            }
            var boundaryIndex = line.IsComplete
                ? MBoxMimeReader.FindBoundary(state.ActiveBoundaries, lineBytes, out _)
                : -1;
            if (boundaryIndex >= 0) {
                state.SetPendingLine(line, lineBytes);
                break;
            }
            var kept = lineBytes.Length;
            if (line.IsComplete == false) {
                wasTruncated = true;
                var trailingCarriageReturnIsAmbiguous = line.EndsWithCarriageReturn && kept > 0;
                if (trailingCarriageReturnIsAmbiguous) {
                    kept--;
                }
            }
            AppendBodyLine(state, buffer, lineBytes, kept, line.TerminatorLength);
            if (line.IsComplete == false) {
                break;
            }
        }
        var part = MBoxPartFactory.Create(
            contentType: entity.ContentType,
            transferEncoding: entity.TransferEncoding,
            disposition: entity.Disposition,
            fileName: entity.FileName,
            contentId: entity.ContentId,
            rawBytes: buffer.ToArray(),
            isAttachment: entity.IsAttachment,
            state: state,
            depth: depth,
            isTruncated: wasTruncated);
        parts.Add(part);
        return part;
    }

    /// <summary>
    /// Appends a stored body line to the bytes of a part, after removing the quoting that the
    /// mailbox applies to it.
    /// </summary>
    /// <param name="state">
    /// The state of the read.
    /// </param>
    /// <param name="buffer">
    /// The buffer that receives the line.
    /// </param>
    /// <param name="lineBytes">
    /// The bytes of the line.
    /// </param>
    /// <param name="kept">
    /// The number of bytes of the line that were kept.
    /// </param>
    internal static void AppendBodyLine(ReadState state,
                                        MBoxByteBuffer buffer,
                                        byte[] lineBytes,
                                        int kept,
                                        int terminatorLength) {
        var start = 0;
        if (MBoxUnquote.IsQuoted(lineBytes.AsSpan(0, kept), state.Record.Format)) {
            /*
             * Exactly one quote mark is removed, whatever the number of quote marks of the line
             * is.
             */
            start = 1;
        }
        buffer.Append(lineBytes.AsSpan(start, kept - start));
        if (terminatorLength > 0) {
            buffer.Append("\n"u8);
        }
    }

    /// <summary>
    /// Holds the state of a body read.
    /// </summary>
    internal sealed class ReadState {
        private PendingLine _PendingLine;

        private sealed record PendingLine(MBoxLine Line, byte[] Bytes);

        /// <summary>
        /// Gets or sets the reader that reads the body.
        /// </summary>
        public MBoxLineReader Reader { get; init; }

        /// <summary>
        /// Gets or sets the record being read.
        /// </summary>
        public MBoxRecord Record { get; init; }

        /// <summary>
        /// Gets or sets the options that control how the body is read.
        /// </summary>
        public MBoxParserOptions Options { get; init; }

        /// <summary>
        /// Gets or sets the list that receives the problems found while reading.
        /// </summary>
        public List<MBoxDiagnostic> Diagnostics { get; init; }

        /// <summary>
        /// Gets or sets the token used to cancel the read.
        /// </summary>
        public CancellationToken Token { get; init; }

        /// <summary>
        /// Gets or sets the number of stored bytes that were inspected.
        /// </summary>
        public long Consumed { get; set; }

        /// <summary>
        /// Gets or sets whether a limit cut the body short.
        /// </summary>
        public bool LimitReached { get; set; }

        public bool InputLimitReached { get; set; }

        public bool PartLimitReached { get; set; }

        public bool StopReading { get; set; }

        public int PartCount { get; set; }

        public int RetainedTextCharacters { get; set; }

        public long RetainedTextBytes { get; set; }

        public List<string> ActiveBoundaries { get; } = new();

        public bool TryReadLine(out MBoxLine line, out byte[] lineBytes) {
            Token.ThrowIfCancellationRequested();
            if (_PendingLine is not null) {
                line = _PendingLine.Line;
                lineBytes = _PendingLine.Bytes;
                _PendingLine = null;
                return true;
            }
            if (StopReading) {
                line = default;
                lineBytes = Array.Empty<byte>();
                return false;
            }
            var remainingInputBytes = Options.MaxPreviewInputBytes - Consumed;
            if (remainingInputBytes <= 0) {
                StopReading = true;
                line = default;
                lineBytes = Array.Empty<byte>();
                if (Reader.Position < Record.MessageOffset + Record.MessageLength) {
                    InputLimitReached = true;
                    LimitReached = true;
                }
                return false;
            }
            var maxReadBytes = (int)Math.Min(remainingInputBytes, int.MaxValue);
            if (Reader.TryReadLine(maxReadBytes, maxReadBytes, out line, Token) == false) {
                lineBytes = Array.Empty<byte>();
                return false;
            }
            Consumed += line.Length + line.TerminatorLength;
            lineBytes = Reader.Line.AsSpan(0, line.Kept).ToArray();
            if (line.IsComplete == false) {
                InputLimitReached = true;
                LimitReached = true;
                StopReading = true;
            }
            return true;
        }

        public MBoxHeaderReadResult ReadHeaderBlock() {
            Token.ThrowIfCancellationRequested();
            var remainingInputBytes = Options.MaxPreviewInputBytes - Consumed;
            if (remainingInputBytes <= 0) {
                StopReading = true;
                var isBeforeRecordEnd = Reader.Position < Record.MessageOffset + Record.MessageLength;
                if (isBeforeRecordEnd) {
                    InputLimitReached = true;
                    LimitReached = true;
                }
                return new MBoxHeaderReadResult(
                    new MBoxHeaders(Array.Empty<MBoxHeader>()),
                    0,
                    false,
                    isBeforeRecordEnd);
            }
            var maxBytes = (int)Math.Min(Options.MaxHeaderBytes, remainingInputBytes);
            var result = MBoxParser.ReadHeaderBlock(
                Reader,
                maxBytes,
                Options.MaxHeaderCount,
                Diagnostics,
                Token);
            Consumed += result.BytesConsumed;
            if (result.IsComplete == false) {
                StopReading = true;
                LimitReached |= result.LimitReached;
                var inputBudgetWasReached = result.LimitReached &&
                                            remainingInputBytes <= Options.MaxHeaderBytes;
                if (inputBudgetWasReached) {
                    InputLimitReached = true;
                }
            }
            return result;
        }

        public void SetPendingLine(MBoxLine line, byte[] lineBytes) {
            _PendingLine = new PendingLine(line, (byte[])lineBytes.Clone());
        }

        public bool TryAddPart() {
            if (PartCount >= Options.MaxPartCount) {
                PartLimitReached = true;
                LimitReached = true;
                StopReading = true;
                Diagnostics.Add(new MBoxDiagnostic(
                    MBoxDiagnosticCode.LimitReached,
                    Reader.Position,
                    "The MIME body contains more parts than the configured limit allows."));
                return false;
            }
            PartCount++;
            return true;
        }

    }
}
