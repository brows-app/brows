using Domore.Text;
using System.Collections.Generic;

namespace Brows.Entries;

/// <summary>
/// Specifies content size limits, text decoding, preview handlers, and media playback settings.
/// </summary>
public interface IEntryStreamGuiOptions {
    /// <summary>
    /// Gets the maximum source length in bytes for the text view, or null for no size limit.
    /// </summary>
    long? TextSourceLengthMax { get; }

    /// <summary>
    /// Gets the maximum source length in bytes for the image view, or null for no size limit.
    /// </summary>
    long? ImageSourceLengthMax { get; }

    /// <summary>
    /// Gets the settings used to decode text from the source stream.
    /// </summary>
    DecodedTextOptions TextDecoderOptions { get; }

    /// <summary>
    /// Gets the maximum source length in bytes for the Markdown view, or null for no size limit.
    /// </summary>
    long? MarkdownSourceLengthMax { get; }

    /// <summary>
    /// Gets the settings used to decode Markdown from the source stream.
    /// </summary>
    DecodedTextOptions MarkdownDecoderOptions { get; }

    /// <summary>
    /// Gets preview handler class identifiers indexed by file extension.
    /// </summary>
    IReadOnlyDictionary<string, Guid> PreviewCLSID { get; }

    /// <summary>
    /// Gets whether video repeats when playback reaches its end.
    /// </summary>
    bool LoopVideo { get; }

    /// <summary>
    /// Gets whether audio repeats when playback reaches its end.
    /// </summary>
    bool LoopAudio { get; }

    /// <summary>
    /// Gets whether video starts playing when loaded.
    /// </summary>
    bool AutoplayVideo { get; }

    /// <summary>
    /// Gets whether audio starts playing when loaded.
    /// </summary>
    bool AutoplayAudio { get; }

    /// <summary>
    /// Gets whether playback is muted.
    /// </summary>
    bool MediaMuted { get; }

    /// <summary>
    /// Gets the playback speed relative to normal speed, where 1 is normal.
    /// </summary>
    double MediaSpeedRatio { get; }

    /// <summary>
    /// Gets the playback volume, from 0 for silence to 1 for full volume.
    /// </summary>
    double MediaVolume { get; }
}
