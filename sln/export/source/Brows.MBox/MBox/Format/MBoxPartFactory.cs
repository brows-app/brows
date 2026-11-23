using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Brows.MBox.Format;

/// <summary>
/// Creates a MIME part from its raw text.
/// </summary>
internal static class MBoxPartFactory {
    internal static MBoxPart Create(MBoxContentType contentType,
                                   string transferEncoding,
                                   string disposition,
                                   string fileName,
                                   string contentId,
                                   byte[] rawBytes,
                                   bool isAttachment,
                                   MBoxBodyReader.ReadState state,
                                   int depth,
                                   bool isTruncated) {
        var diagnostics = new List<MBoxDiagnostic>();
        var charset = contentType.Charset;
        var isText = contentType.IsText;
        var isHtml = string.Equals(contentType.Subtype, "html", StringComparison.OrdinalIgnoreCase);
        var bodyState = MBoxBodyState.Complete;
        string text = null;
        long? decodedLength = null;
        var partIsNotInlineText = isAttachment || isText == false;
        if (partIsNotInlineText) {
            /*
             * A binary attachment is not decoded. Its bytes are streamed by the extraction.
             */
            bodyState = isAttachment ? MBoxBodyState.NotRead : MBoxBodyState.Unavailable;
        }
        else {
            var bytes = DecodeTransfer(rawBytes, transferEncoding, diagnostics, state);
            decodedLength = bytes is null ? null : bytes.Length;
            if (bytes is null) {
                bodyState = MBoxBodyState.Unavailable;
            }
            else {
                if (diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedEncoding)) {
                    bodyState = MBoxBodyState.Malformed;
                }
                text = DecodeCharset(bytes, charset, diagnostics, state, out var malformed);
                if (malformed) {
                    bodyState = MBoxBodyState.Malformed;
                }
                if (text is not null) {
                    text = ApplyFlowed(text, contentType, diagnostics, state);
                }
                if (text is not null) {
                    text = Limit(text, state, diagnostics, ref bodyState);
                }
            }
        }
        var completedPartWasTruncated = isTruncated && bodyState == MBoxBodyState.Complete;
        if (completedPartWasTruncated) {
            bodyState = MBoxBodyState.Truncated;
        }
        state.Diagnostics.AddRange(diagnostics);
        return new MBoxPart(
            contentType: contentType,
            charset: charset,
            transferEncoding: transferEncoding,
            disposition: disposition,
            fileName: fileName,
            contentId: contentId,
            rawLength: rawBytes is null ? null : (long?)rawBytes.Length,
            decodedLength: decodedLength,
            text: text,
            state: bodyState,
            diagnostics: diagnostics) {
            IsAttachmentPart = isAttachment,
        };
    }

    /// <summary>
    /// Applies the limits on the decoded text of a part.
    /// </summary>
    /// <param name="text">
    /// The decoded text.
    /// </param>
    /// <param name="state">
    /// The state of the read.
    /// </param>
    /// <param name="diagnostics">
    /// The list that receives the problems found while limiting the text.
    /// </param>
    /// <param name="bodyState">
    /// The state of the body, which is updated when a limit cuts the text short.
    /// </param>
    /// <returns>
    /// The text that is within the limits.
    /// </returns>
    private static string Limit(string text,
                                MBoxBodyReader.ReadState state,
                                List<MBoxDiagnostic> diagnostics,
                                ref MBoxBodyState bodyState) {
        var remainingCharacters = Math.Max(0, state.Options.MaxPreviewCharacters - state.RetainedTextCharacters);
        var remainingBytes = Math.Max(0L, state.Options.MaxDecodedTextBytes - state.RetainedTextBytes);
        var maxCharacters = (int)Math.Min(remainingCharacters, remainingBytes / sizeof(char));
        if (text.Length > maxCharacters) {
            text = Truncate(text, maxCharacters);
            state.LimitReached = true;
            if (bodyState == MBoxBodyState.Complete) {
                bodyState = MBoxBodyState.Truncated;
            }
            diagnostics.Add(new MBoxDiagnostic(
                MBoxDiagnosticCode.LimitReached,
                state.Reader.Position,
                "The retained text is longer than the aggregate preview limit allows."));
        }
        state.RetainedTextCharacters += text.Length;
        state.RetainedTextBytes += (long)text.Length * sizeof(char);
        return text;
    }

    internal static byte[] DecodeTransfer(byte[] rawBytes,
                                         string transferEncoding,
                                         List<MBoxDiagnostic> diagnostics,
                                         MBoxBodyReader.ReadState state,
                                         bool keepLineBreaks = false) {
        if (string.IsNullOrEmpty(transferEncoding)) {
            return rawBytes;
        }
        var encoding = transferEncoding.Trim();
        var isIdentityEncoding = encoding.Equals("7bit", StringComparison.OrdinalIgnoreCase) ||
                                 encoding.Equals("8bit", StringComparison.OrdinalIgnoreCase) ||
                                 encoding.Equals("binary", StringComparison.OrdinalIgnoreCase);
        if (isIdentityEncoding) {
            return rawBytes;
        }
        if (encoding.Equals("base64", StringComparison.OrdinalIgnoreCase)) {
            return MBoxTransfer.DecodeBase64(rawBytes, diagnostics, state);
        }
        if (encoding.Equals("quoted-printable", StringComparison.OrdinalIgnoreCase)) {
            return MBoxTransfer.DecodeQuotedPrintable(rawBytes, diagnostics, state, keepLineBreaks);
        }
        diagnostics.Add(new MBoxDiagnostic(
            MBoxDiagnosticCode.UnsupportedBody,
            state.Reader.Position,
            $"The content transfer encoding [{encoding}] is not supported."));
        return null;
    }

    private static string DecodeCharset(byte[] bytes,
                                        string charset,
                                        List<MBoxDiagnostic> diagnostics,
                                        MBoxBodyReader.ReadState state,
                                        out bool malformed) {
        malformed = false;
        if (string.IsNullOrWhiteSpace(charset)) {
            /*
             * An absent character set is read as UTF-8, then as Latin-1.
             */
            var strict = DecodeStrict(bytes, new UTF8Encoding(false, true));
            if (strict is not null) {
                return strict;
            }
            return DecodeStrict(bytes, Encoding.Latin1);
        }
        var encoding = MBoxCharset.GetEncoding(charset);
        if (encoding is null) {
            diagnostics.Add(new MBoxDiagnostic(
                MBoxDiagnosticCode.UnsupportedCharset,
                state.Reader.Position,
                $"The character set [{charset}] is not supported."));
            var fallback = DecodeStrict(bytes, new UTF8Encoding(false, true));
            if (fallback is not null) {
                return fallback;
            }
            return DecodeStrict(bytes, Encoding.Latin1);
        }
        var text = DecodeStrict(bytes, encoding);
        if (text is not null) {
            return text;
        }
        /*
         * A known character set that fails a strict decode is decoded with replacement, and the
         * result is marked as malformed.
         */
        malformed = true;
        diagnostics.Add(new MBoxDiagnostic(
            MBoxDiagnosticCode.MalformedEncoding,
            state.Reader.Position,
            $"The text is not valid [{charset}] and was decoded with replacement characters."));
        try {
            var replacement = (Encoding)encoding.Clone();
            replacement.DecoderFallback = DecoderFallback.ReplacementFallback;
            return replacement.GetString(bytes);
        }
        catch (DecoderFallbackException) {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    private static string DecodeStrict(byte[] bytes, Encoding encoding) {
        try {
            return encoding.GetString(bytes);
        }
        catch (DecoderFallbackException) {
            return null;
        }
    }

    private static string ApplyFlowed(string text,
                                      MBoxContentType contentType,
                                      List<MBoxDiagnostic> diagnostics,
                                      MBoxBodyReader.ReadState state) {
        if (string.Equals(contentType.Subtype, "plain", StringComparison.OrdinalIgnoreCase) == false) {
            return text;
        }
        if (contentType.Parameters.TryGetValue("format", out var format) == false) {
            return text;
        }
        if (string.Equals(format, "flowed", StringComparison.OrdinalIgnoreCase) == false) {
            return text;
        }
        var delSp = false;
        if (contentType.Parameters.TryGetValue("delsp", out var delSpValue)) {
            delSp = string.Equals(delSpValue, "yes", StringComparison.OrdinalIgnoreCase);
        }
        return MBoxFlowed.Decode(text, delSp);
    }

    private static string Truncate(string text, int limit) {
        /*
         * The truncation never cuts a surrogate pair in half.
         */
        var end = limit;
        var wouldSplitSurrogatePair = end < text.Length && end > 0 && char.IsHighSurrogate(text[end - 1]);
        if (wouldSplitSurrogatePair) {
            end--;
        }
        return text[..end];
    }
}
