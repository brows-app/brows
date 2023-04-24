using System.Collections.Generic;

namespace Brows {
    public sealed class ZipArchiveUpdate {
        public HashSet<string> DeleteEntries {
            get => _DeleteEntries ?? (_DeleteEntries = new());
            set => _DeleteEntries = value;
        }
        private HashSet<string> _DeleteEntries;

        public Dictionary<string, string> CreateEntriesFromFiles {
            get => _CreateEntriesFromFiles ?? (_CreateEntriesFromFiles = new());
            set => _CreateEntriesFromFiles = value;
        }
        private Dictionary<string, string> _CreateEntriesFromFiles;

        public List<IEntryStreamSet> CopyStreamSets {
            get => _CopyStreamSets ?? (_CopyStreamSets = new());
            set => _CopyStreamSets = value;
        }
        private List<IEntryStreamSet> _CopyStreamSets;
    }
}
