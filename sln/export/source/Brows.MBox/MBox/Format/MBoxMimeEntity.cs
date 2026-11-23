using System;
using System.Collections.Generic;

namespace Brows.MBox.Format;

internal sealed record MBoxMimeEntity {
    private MBoxMimeEntity(MBoxContentType contentType,
                           string transferEncoding,
                           string disposition,
                           string fileName,
                           string contentId,
                           bool isAttachment) {
        ContentType = contentType;
        TransferEncoding = transferEncoding;
        Disposition = disposition;
        FileName = fileName;
        ContentId = contentId;
        IsAttachment = isAttachment;
    }

    internal MBoxContentType ContentType { get; }

    internal string TransferEncoding { get; }

    internal string Disposition { get; }

    internal string FileName { get; }

    internal string ContentId { get; }

    internal bool IsAttachment { get; }

    internal static MBoxMimeEntity Create(MBoxContentType contentType,
                                        MBoxHeaders headers,
                                        MBoxMimeEntity parent) {
        var contentTypeValue = headers.Get("Content-Type")?.RawValue;
        var ownDisposition = headers.Get("Content-Disposition")?.RawValue;
        var disposition = ownDisposition ?? parent?.Disposition;
        var fileName = FindParameter(ownDisposition, "filename") ??
                       FindParameter(contentTypeValue, "name") ??
                       parent?.FileName;
        var contentId = headers.Get("Content-ID")?.RawValue ?? parent?.ContentId;
        var dispositionIsAttachment = IsAttachmentDisposition(ownDisposition);
        var isAttachment = parent?.IsAttachment == true ||
                           dispositionIsAttachment ||
                           string.IsNullOrEmpty(fileName) == false;
        return new MBoxMimeEntity(
            contentType ?? MBoxContentTypes.TextPlain,
            headers.Get("Content-Transfer-Encoding")?.RawValue,
            disposition,
            fileName,
            contentId,
            isAttachment);
    }

    internal static MBoxMimeEntity Create(MBoxHeaders headers,
                                        MBoxMimeEntity parent,
                                        List<MBoxDiagnostic> diagnostics,
                                        long offset) {
        var contentType = MBoxContentTypeParser.Parse(
            headers.Get("Content-Type")?.RawValue,
            diagnostics,
            offset);
        return Create(contentType, headers, parent);
    }

    private static bool IsAttachmentDisposition(string disposition) {
        if (string.IsNullOrWhiteSpace(disposition)) {
            return false;
        }
        var values = MBoxContentTypeParser.SplitParameters(disposition);
        return string.Equals(values[0].Trim(), "attachment", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindParameter(string value, string name) {
        if (string.IsNullOrEmpty(value)) {
            return null;
        }
        var parts = MBoxContentTypeParser.SplitParameters(value);
        for (var i = 1; i < parts.Count; i++) {
            var equals = parts[i].IndexOf('=');
            if (equals < 0) {
                continue;
            }
            var parameterName = parts[i][..equals].Trim();
            if (string.Equals(parameterName, name, StringComparison.OrdinalIgnoreCase) == false) {
                continue;
            }
            var parameterValue = parts[i][(equals + 1)..].Trim();
            var parameterIsQuoted = parameterValue.Length >= 2 &&
                                    parameterValue[0] == '"' && parameterValue[^1] == '"';
            if (parameterIsQuoted) {
                return parameterValue[1..^1];
            }
            return parameterValue;
        }
        return null;
    }
}
