using System;
using System.Collections.Generic;
using System.Text;

namespace Brows.MBox.Format;

/// <summary>
/// Reads MIME entities and selects the text to display.
/// </summary>
internal static class MBoxMimeReader {
    private static void AddMalformedBoundary(MBoxBodyReader.ReadState state, string message) {
        state.Diagnostics.Add(new MBoxDiagnostic(
            MBoxDiagnosticCode.MalformedMime,
            state.Reader.Position,
            message));
    }

    private static MBoxBodyState CombineState(MBoxBodyState first, MBoxBodyState second) {
        var selectedStateIsMalformed = first == MBoxBodyState.Malformed ||
                                       second == MBoxBodyState.Malformed;
        if (selectedStateIsMalformed) {
            return MBoxBodyState.Malformed;
        }
        var selectedStateIsTruncated = first == MBoxBodyState.Truncated ||
                                       second == MBoxBodyState.Truncated;
        if (selectedStateIsTruncated) {
            return MBoxBodyState.Truncated;
        }
        var selectedStateIsComplete = first == MBoxBodyState.Complete ||
                                      second == MBoxBodyState.Complete;
        if (selectedStateIsComplete) {
            return MBoxBodyState.Complete;
        }
        var selectedStateWasNotRead = first == MBoxBodyState.NotRead ||
                                      second == MBoxBodyState.NotRead;
        if (selectedStateWasNotRead) {
            return MBoxBodyState.NotRead;
        }
        return MBoxBodyState.Unavailable;
    }

    private static MBoxSelection SelectMultipart(MBoxBodyReader.ReadState state,
                                                  MBoxContentType contentType,
                                                  List<MBoxSelection> children) {
        var subtype = contentType.Subtype;
        if (string.Equals(subtype, "alternative", StringComparison.OrdinalIgnoreCase)) {
            /*
             * The last inline plain text alternative and the last inline HTML alternative are
             * selected. Alternatives are never concatenated.
             */
            MBoxSelection text = null;
            MBoxSelection html = null;
            foreach (var child in children) {
                if (child.IsAttachment) {
                    continue;
                }
                if (child.HasText) {
                    text = child;
                }
                if (child.HasHtml) {
                    html = child;
                }
            }
            var hasTextOrHtmlSelection = text is not null || html is not null;
            if (hasTextOrHtmlSelection) {
                var selectionState = text is null
                    ? html.State
                    : html is null
                        ? text.State
                        : CombineState(text.State, html.State);
                return new MBoxSelection {
                    Text = text?.Text,
                    Html = html?.Html,
                    State = selectionState,
                    ContentId = text?.ContentId ?? html?.ContentId,
                };
            }
            return MBoxSelection.Empty;
        }
        if (string.Equals(subtype, "related", StringComparison.OrdinalIgnoreCase)) {
            /*
             * The root of a related part is the part that the start parameter refers to, and
             * otherwise the first child. The other children are resources of the root.
             */
            contentType.Parameters.TryGetValue("start", out var rootId);
            if (string.IsNullOrEmpty(rootId) == false) {
                foreach (var child in children) {
                    if (string.Equals(child.ContentId, rootId, StringComparison.OrdinalIgnoreCase)) {
                        return child;
                    }
                }
                state.Diagnostics.Add(new MBoxDiagnostic(
                    MBoxDiagnosticCode.MalformedMime,
                    state.Reader.Position,
                    "The related part refers to a root that is not present."));
                return MBoxSelection.Empty;
            }
            foreach (var child in children) {
                if (child.IsAttachment == false) {
                    return child;
                }
            }
            return MBoxSelection.Empty;
        }
        if (string.Equals(subtype, "encrypted", StringComparison.OrdinalIgnoreCase)) {
            state.Diagnostics.Add(new MBoxDiagnostic(
                MBoxDiagnosticCode.UnsupportedBody,
                state.Reader.Position,
                "The encrypted message body cannot be displayed."));
            return MBoxSelection.Empty;
        }
        /*
         * The multipart types that are not handled separately are treated like multipart/mixed:
         * the first child that yields a display body is selected, and the later siblings cannot
         * replace it.
         */
        foreach (var child in children) {
            if (child.IsAttachment) {
                continue;
            }
            if (child.HasDisplayBody) {
                return child;
            }
        }
        return MBoxSelection.Empty;
    }

    private static bool IsDelimiter(byte[] line, string boundary, out bool isFinal) {
        isFinal = false;
        var lineHasBoundaryPrefix = line.Length >= 2 &&
                                    line[0] == (byte)'-' &&
                                    line[1] == (byte)'-';
        if (lineHasBoundaryPrefix == false) {
            return false;
        }
        var boundaryBytes = Encoding.ASCII.GetBytes(boundary);
        if (line.Length < boundaryBytes.Length + 2) {
            return false;
        }
        for (var i = 0; i < boundaryBytes.Length; i++) {
            if (line[i + 2] != boundaryBytes[i]) {
                return false;
            }
        }
        var index = boundaryBytes.Length + 2;
        var lineMightHaveClosingDashes = index < line.Length && line[index] == (byte)'-';
        if (lineMightHaveClosingDashes) {
            /*
             * The closing delimiter ends with two dashes, and both of them are part of it.
             */
            var hasSecondClosingDash = index + 1 < line.Length && line[index + 1] == (byte)'-';
            if (hasSecondClosingDash == false) {
                return false;
            }
            isFinal = true;
            index += 2;
        }
        while (true) {
            var hasTrailingWhitespace = index < line.Length &&
                                       (line[index] == (byte)' ' || line[index] == (byte)'\t');
            if (hasTrailingWhitespace == false) {
                break;
            }
            index++;
        }
        return index == line.Length;
    }

    internal static int FindBoundary(List<string> boundaries, byte[] line, out bool isFinal) {
        isFinal = false;
        for (var i = boundaries.Count - 1; i >= 0; i--) {
            if (IsDelimiter(line, boundaries[i], out isFinal)) {
                return i;
            }
        }
        return -1;
    }

    internal static MBoxSelection MarkMalformed(MBoxSelection selection) {
        if (selection is null) {
            return new MBoxSelection { State = MBoxBodyState.Malformed };
        }
        return new MBoxSelection {
            Text = selection.Text,
            Html = selection.Html,
            State = MBoxBodyState.Malformed,
            IsAttachment = selection.IsAttachment,
            ContentId = selection.ContentId,
        };
    }

    internal static MBoxSelection ReadMultipart(MBoxBodyReader.ReadState state,
                                              MBoxMimeEntity entity,
                                              List<MBoxPart> parts,
                                              int depth) {
        if (depth >= state.Options.MaxMimeDepth) {
            state.Diagnostics.Add(new MBoxDiagnostic(
                MBoxDiagnosticCode.LimitReached,
                state.Reader.Position,
                "The MIME part tree is deeper than the configured limit allows."));
            state.LimitReached = true;
            state.StopReading = true;
            return MBoxSelection.Empty;
        }
        var boundary = entity.ContentType.Boundary;
        if (string.IsNullOrEmpty(boundary)) {
            AddMalformedBoundary(state, "The multipart body has no boundary parameter.");
            return MarkMalformed(MBoxSelection.Empty);
        }
        state.ActiveBoundaries.Add(boundary);
        var children = new List<MBoxSelection>();
        var foundEnd = false;
        var structuralMalformed = false;
        try {
            while (state.StopReading == false) {
                state.Token.ThrowIfCancellationRequested();
                if (state.TryReadLine(out var line, out var lineBytes) == false) {
                    break;
                }
                if (line.IsComplete == false) {
                    break;
                }
                var boundaryIndex = FindBoundary(state.ActiveBoundaries, lineBytes, out var isFinal);
                if (boundaryIndex < 0) {
                    /*
                     * Preamble and epilogue lines are not displayed. A leaf reader returns its
                     * delimiter to this loop, so all ordinary lines here belong to one of them.
                     */
                    continue;
                }
                if (boundaryIndex < state.ActiveBoundaries.Count - 1) {
                    state.SetPendingLine(line, lineBytes);
                    structuralMalformed = true;
                    AddMalformedBoundary(
                        state,
                        "The multipart body ends at an enclosing boundary without its own closing boundary.");
                    break;
                }
                if (isFinal) {
                    foundEnd = true;
                    break;
                }
                var diagnosticStart = state.Diagnostics.Count;
                var headerResult = state.ReadHeaderBlock();
                if (headerResult.IsComplete == false) {
                    if (headerResult.LimitReached == false) {
                        structuralMalformed = true;
                        AddMalformedBoundary(state, "A MIME part header block ends without an empty line.");
                    }
                    break;
                }
                var headersAreMalformed = false;
                for (var i = diagnosticStart; i < state.Diagnostics.Count; i++) {
                    if (state.Diagnostics[i].Code == MBoxDiagnosticCode.MalformedHeader) {
                        headersAreMalformed = true;
                        break;
                    }
                }
                var entityDiagnosticStart = state.Diagnostics.Count;
                var childEntity = MBoxMimeEntity.Create(
                    headerResult.Headers,
                    entity,
                    state.Diagnostics,
                    state.Reader.Position);
                for (var i = entityDiagnosticStart; i < state.Diagnostics.Count; i++) {
                    if (state.Diagnostics[i].Code == MBoxDiagnosticCode.MalformedMime) {
                        headersAreMalformed = true;
                        break;
                    }
                }
                if (state.TryAddPart() == false) {
                    break;
                }
                MBoxSelection childSelection;
                if (childEntity.ContentType.IsMultipart) {
                    if (string.IsNullOrEmpty(childEntity.ContentType.Boundary)) {
                        AddMalformedBoundary(state, "A multipart MIME part has no boundary parameter.");
                        childSelection = MarkMalformed(MBoxSelection.Empty);
                    }
                    else {
                        childSelection = ReadMultipart(state, childEntity, parts, depth + 1);
                    }
                    childSelection = MBoxSelection.WithEntityContext(
                        childSelection,
                        childEntity.IsAttachment,
                        childEntity.ContentId);
                }
                else {
                    var part = MBoxBodyReader.ReadLeaf(state, childEntity, parts, depth);
                    childSelection = MBoxSelection.Of(part);
                }
                if (headersAreMalformed) {
                    childSelection = MarkMalformed(childSelection);
                }
                children.Add(childSelection);
            }
        }
        finally {
            state.ActiveBoundaries.RemoveAt(state.ActiveBoundaries.Count - 1);
        }
        var multipartEndedWithoutClose = foundEnd == false &&
                                         state.StopReading == false &&
                                         structuralMalformed == false;
        if (multipartEndedWithoutClose) {
            structuralMalformed = true;
            AddMalformedBoundary(
                state,
                "The multipart body ends without its closing boundary.");
        }
        var selection = SelectMultipart(state, entity.ContentType, children);
        if (structuralMalformed) {
            selection = MarkMalformed(selection);
        }
        return MBoxSelection.WithEntityContext(selection, entity.IsAttachment, entity.ContentId);
    }
}
