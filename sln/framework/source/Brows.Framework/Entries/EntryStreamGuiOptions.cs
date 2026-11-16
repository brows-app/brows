using Domore.Text;
using System.Collections.Generic;

namespace Brows.Entries;

public class EntryStreamGuiOptions : IEntryStreamGuiOptions {
    private TextOptions TextInit() {
        var opt = new TextOptions();
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

    public TextOptions Text {
        get => field ??= TextInit();
        set;
    }

    public ImageOptions Image {
        get => field ??= new();
        set;
    }

    public MediaOptions Media {
        get => field ??= new();
        set;
    }

    public PreviewOptions Preview {
        get => field ??= new();
        set;
    }

    public sealed class ExtensionSet : HashSet<string> {
        public ExtensionSet(params string[] collection) : base(collection, StringComparer.OrdinalIgnoreCase) {
        }
    }

    public sealed class ImageOptions {
        public long? SourceLengthMax { get; set; } = 10000000;

        public ExtensionSet Extensions {
            get => field ??= new(".bmp", ".jpg", ".jpeg", ".gif", ".heif", ".heic", ".png", ".tif", ".tiff");
            set;
        }
    }

    public sealed class TextOptions : DecodedTextOptions {
        public long? SourceLengthMax { get; set; } = 1000000;

        public ExtensionSet Extensions {
            get => field ??= new(".txt");
            set;
        }
    }

    public sealed class PreviewOptions {
        public Dictionary<string, Guid> CLSID {
            get => field ??= new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            set;
        }

        public ExtensionSet Extensions {
            get => field ??= new(".pdf");
            set;
        }
    }

    public sealed class MediaOptions {
        public ExtensionSet Extensions {
            get => field ??= new(".3gp", ".avi", ".mov", ".mp4", ".mkv");
            set;
        }

        public bool LoopAudio { get; set; } = true;
        public bool LoopVideo { get; set; } = true;
        public bool AutoplayAudio { get; set; } = false;
        public bool AutoplayVideo { get; set; } = true;
        public double SpeedRatio { get; set; } = 1;
        public double Volume { get; set; } = 0.5;
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

    bool IEntryStreamGuiOptions.LoopAudio => Media.LoopAudio;
    bool IEntryStreamGuiOptions.LoopVideo => Media.LoopVideo;
    bool IEntryStreamGuiOptions.AutoplayAudio => Media.AutoplayAudio;
    bool IEntryStreamGuiOptions.AutoplayVideo => Media.AutoplayVideo;
    bool IEntryStreamGuiOptions.MediaMuted => Media.Muted;
    double IEntryStreamGuiOptions.MediaVolume => Media.Volume;
    double IEntryStreamGuiOptions.MediaSpeedRatio => Media.SpeedRatio;
}
