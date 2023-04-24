using System.Collections.Generic;

namespace Brows {
    public sealed class ZipArchiveRead {
        public bool ExtractOverwrites { get; set; }

        public Dictionary<string, string> ExtractEntriesToFiles {
            get => _ExtractEntriesToFiles ?? (_ExtractEntriesToFiles = new());
            set => _ExtractEntriesToFiles = value;
        }
        private Dictionary<string, string> _ExtractEntriesToFiles;
    }
}
