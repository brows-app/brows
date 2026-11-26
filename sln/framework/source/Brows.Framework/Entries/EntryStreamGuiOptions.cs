using Domore.Text;
using System.Collections.Generic;

namespace Brows.Entries;

/// <summary>
/// Configures how entry content is displayed as text, Markdown, images, media, or a platform preview.
/// </summary>
public class EntryStreamGuiOptions : IEntryStreamGuiOptions {
    private static TextOptions TextInit() {
        var
        opt = new TextOptions();
        opt.Encoding.Clear();
        opt.Encoding.Add("utf-8");
        opt.EncodingFallback.Clear();
        opt.StreamBuffer.Size = 1024;
        opt.StreamBuffer.Clear = false;
        opt.StreamBuffer.Shared = true;
        opt.TextBuffer.Size = 1024;
        opt.TextBuffer.Clear = false;
        opt.TextBuffer.Shared = true;
        return opt;
    }

    /// <summary>
    /// Gets or sets the text display and decoding options.
    /// </summary>
    public TextOptions Text {
        get => field ??= TextInit();
        set;
    }

    /// <summary>
    /// Gets or sets the Markdown display and decoding options.
    /// </summary>
    public MarkdownOptions Markdown {
        get => field ??= new();
        set;
    }

    /// <summary>
    /// Gets or sets the image display options.
    /// </summary>
    public ImageOptions Image {
        get => field ??= new();
        set;
    }

    /// <summary>
    /// Gets or sets the audio and video playback options.
    /// </summary>
    public MediaOptions Media {
        get => field ??= new();
        set;
    }

    /// <summary>
    /// Gets or sets the platform preview options.
    /// </summary>
    public PreviewOptions Preview {
        get => field ??= new();
        set;
    }

    /// <summary>
    /// Provides a set of file extensions compared without regard to letter case.
    /// </summary>
    public sealed class ExtensionSet : HashSet<string> {
        /// <summary>
        /// Initializes a case-insensitive set of file extensions.
        /// </summary>
        /// <param name="collection">
        /// The extensions to include, including their leading dots.
        /// </param>
        public ExtensionSet(params string[] collection) : base(collection, StringComparer.OrdinalIgnoreCase) {
        }
    }

    /// <summary>
    /// Configures image size limits and recognized extensions.
    /// </summary>
    public sealed class ImageOptions {
        /// <summary>
        /// Gets or sets the maximum source length in bytes for the image view, or null for no size limit.
        /// </summary>
        public long? SourceLengthMax { get; set; } = 10000000;

        /// <summary>
        /// Gets or sets the file extensions recognized for the image view.
        /// </summary>
        public ExtensionSet Extensions {
            get => field ??= new(".bmp", ".jpg", ".jpeg", ".gif", ".heif", ".heic", ".png", ".tif", ".tiff");
            set;
        }
    }

    /// <summary>
    /// Configures text decoding, size limits, and recognized extensions.
    /// </summary>
    public sealed class TextOptions : DecodedTextOptions {
        /// <summary>
        /// Gets or sets the maximum source length in bytes for the text view, or null for no size limit.
        /// </summary>
        public long? SourceLengthMax { get; set; } = 1000000;

        /// <summary>
        /// Gets or sets the file extensions recognized for the text view.
        /// </summary>
        public ExtensionSet Extensions {
            get => field ??= new(".txt");
            set;
        }
    }

    /// <summary>
    /// Configures Markdown decoding, size limits, and recognized extensions.
    /// </summary>
    public sealed class MarkdownOptions : DecodedTextOptions {
        /// <summary>
        /// Gets or sets the maximum source length in bytes for the Markdown view, or null for no size limit.
        /// </summary>
        public long? SourceLengthMax { get; set; } = 1000000;

        /// <summary>
        /// Gets or sets the file extensions recognized for the Markdown view.
        /// </summary>
        public ExtensionSet Extensions {
            get => field ??= new(".md");
            set;
        }
    }

    /// <summary>
    /// Configures platform preview handlers and recognized extensions.
    /// </summary>
    public sealed class PreviewOptions {
        /// <summary>
        /// Gets or sets the preview handler class identifiers indexed by file extension.
        /// </summary>
        public Dictionary<string, Guid> CLSID {
            get => field ??= new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            set;
        }

        /// <summary>
        /// Gets or sets the file extensions recognized for platform previews.
        /// </summary>
        public ExtensionSet Extensions {
            get => field ??= new(".pdf");
            set;
        }
    }

    /// <summary>
    /// Configures audio and video playback and recognized extensions.
    /// </summary>
    public sealed class MediaOptions {
        /// <summary>
        /// Gets or sets the file extensions recognized for the media view.
        /// </summary>
        public ExtensionSet Extensions {
            get => field ??= new(".3gp", ".avi", ".mov", ".mp4", ".mkv");
            set;
        }

        /// <summary>
        /// Gets or sets whether audio repeats when playback reaches its end.
        /// </summary>
        public bool LoopAudio { get; set; } = true;

        /// <summary>
        /// Gets or sets whether video repeats when playback reaches its end.
        /// </summary>
        public bool LoopVideo { get; set; } = true;

        /// <summary>
        /// Gets or sets whether audio starts playing when loaded.
        /// </summary>
        public bool AutoplayAudio { get; set; } = false;

        /// <summary>
        /// Gets or sets whether video starts playing when loaded.
        /// </summary>
        public bool AutoplayVideo { get; set; } = true;

        /// <summary>
        /// Gets or sets the playback speed relative to normal speed, where 1 is normal.
        /// </summary>
        public double SpeedRatio { get; set; } = 1;

        /// <summary>
        /// Gets or sets the playback volume, from 0 for silence to 1 for full volume.
        /// </summary>
        public double Volume { get; set; } = 0.5;

        /// <summary>
        /// Gets or sets whether playback is muted.
        /// </summary>
        public bool Muted { get; set; } = true;
    }

    long? IEntryStreamGuiOptions.ImageSourceLengthMax =>
        Image.SourceLengthMax;

    IReadOnlyDictionary<string, Guid> IEntryStreamGuiOptions.PreviewCLSID =>
        Preview.CLSID;

    long? IEntryStreamGuiOptions.TextSourceLengthMax =>
        Text.SourceLengthMax;

    DecodedTextOptions IEntryStreamGuiOptions.TextDecoderOptions =>
        Text;

    long? IEntryStreamGuiOptions.MarkdownSourceLengthMax =>
        Markdown.SourceLengthMax;

    DecodedTextOptions IEntryStreamGuiOptions.MarkdownDecoderOptions =>
        Markdown;

    bool IEntryStreamGuiOptions.LoopAudio => Media.LoopAudio;
    bool IEntryStreamGuiOptions.LoopVideo => Media.LoopVideo;
    bool IEntryStreamGuiOptions.AutoplayAudio => Media.AutoplayAudio;
    bool IEntryStreamGuiOptions.AutoplayVideo => Media.AutoplayVideo;
    bool IEntryStreamGuiOptions.MediaMuted => Media.Muted;
    double IEntryStreamGuiOptions.MediaVolume => Media.Volume;
    double IEntryStreamGuiOptions.MediaSpeedRatio => Media.SpeedRatio;
}
