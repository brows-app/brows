﻿using System.Collections.Generic;

namespace Brows {
    public sealed class ZipArchiveUpdate {
        public HashSet<string> DeleteEntries {
            get => _DeleteEntries ??= [];
            set => _DeleteEntries = value;
        }
        private HashSet<string> _DeleteEntries;

        public Dictionary<string, string> CreateEntriesFromFiles {
            get => _CreateEntriesFromFiles ??= [];
            set => _CreateEntriesFromFiles = value;
        }
        private Dictionary<string, string> _CreateEntriesFromFiles;

        public List<IEntryStreamSet> CopyStreamSets {
            get => _CopyStreamSets ??= [];
            set => _CopyStreamSets = value;
        }
        private List<IEntryStreamSet> _CopyStreamSets;
    }
}
