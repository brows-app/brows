using System;
using System.Collections.Generic;

namespace Brows.MBox.Format;

/// <summary>
/// Provides the content types used when a message does not declare one.
/// </summary>
internal static class MBoxContentTypes {
    /// <summary>
    /// Gets the content type used when a message does not declare one.
    /// </summary>
    public static MBoxContentType TextPlain {
        get => field ??= new MBoxContentType(
            type: "text",
            subtype: "plain",
            full: "text/plain",
            boundary: null,
            charset: null,
            parameters: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
