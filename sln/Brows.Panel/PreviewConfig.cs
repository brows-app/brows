using Domore.Text;
using System;
using System.Collections.Generic;

namespace Brows {
    using Gui;

    internal class PreviewConfig : IPreviewConfig {
        public Dictionary<string, Guid> CLSID {
            get => _CLSID ?? (_CLSID = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));
            set => _CLSID = value;
        }
        private Dictionary<string, Guid> _CLSID;

        public DecodedTextOptions TextOptions {
            get => _TextOptions ?? (_TextOptions = new DecodedTextOptions());
            set => _TextOptions = value;
        }
        private DecodedTextOptions _TextOptions;

        public long? TextFileLengthMax { get; set; }

        Guid? IPreviewConfig.CLSID(string extension) {
            return CLSID.TryGetValue(extension, out var value)
                ? value
                : null;
        }

        Guid? IPreviewConfig.CLSID(Guid existing) {
            return CLSID.TryGetValue(existing.ToString(), out var value)
                ? value
                : null;
        }
    }
}
