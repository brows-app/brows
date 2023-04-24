using Domore.Text;
using System;
using System.Collections.Generic;

namespace Brows {
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
            get => _Text ?? (_Text = TextInit());
            set => _Text = value;
        }
        private TextOptions _Text;

        public ImageOptions Image {
            get => _Image ?? (_Image = new());
            set => _Image = value;
        }
        private ImageOptions _Image;

        public PreviewOptions Preview {
            get => _Preview ?? (_Preview = new());
            set => _Preview = value;
        }
        private PreviewOptions _Preview;

        public class ImageOptions {
            public long? SourceLengthMax { get; set; } = 5120000;
        }

        public class TextOptions : DecodedTextOptions {
            public long? SourceLengthMax { get; set; } = 100000;
        }

        public class PreviewOptions {
            public Dictionary<string, Guid> CLSID {
                get => _CLSID ?? (_CLSID = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));
                set => _CLSID = value;
            }
            private Dictionary<string, Guid> _CLSID;
        }

        long? IEntryStreamGuiOptions.ImageSourceLengthMax =>
            Image.SourceLengthMax;

        IReadOnlyDictionary<string, Guid> IEntryStreamGuiOptions.PreviewCLSID =>
            Preview.CLSID;

        long? IEntryStreamGuiOptions.TextSourceLengthMax =>
            Text.SourceLengthMax;

        DecodedTextOptions IEntryStreamGuiOptions.DecodedTextOptions =>
            Text;
    }
}
