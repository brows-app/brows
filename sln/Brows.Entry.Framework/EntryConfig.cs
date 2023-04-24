using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    public class EntryConfig {
        protected virtual IEnumerable<string> DefaultKeyInit() {
            return Array.Empty<string>();
        }

        protected virtual IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSortInit() {
            return Array.Empty<KeyValuePair<string, EntrySortDirection>>();
        }

        public IEnumerable<string> DefaultKeys =>
            DefaultKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        public IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSorting =>
            DefaultSort
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s =>
                    s.EndsWith('<') ? KeyValuePair.Create(s[..^1], EntrySortDirection.Ascending) :
                    s.EndsWith('>') ? KeyValuePair.Create(s[..^1], EntrySortDirection.Descending) :
                    KeyValuePair.Create(s, EntrySortDirection.Ascending))
                .GroupBy(pair => pair.Key)
                .Select(group => KeyValuePair.Create(group.Key, group.Last().Value));

        public string DefaultKey {
            get => _DefaultKey ?? (_DefaultKey = string.Join("|", DefaultKeyInit()));
            set => _DefaultKey = value;
        }
        private string _DefaultKey;

        public string DefaultSort {
            get => _DefaultSort ?? (_DefaultSort = string.Join("|", DefaultSortInit()
                .Select(pair => $"{pair.Key}{(
                    pair.Value == EntrySortDirection.Ascending
                        ? "<"
                        : pair.Value == EntrySortDirection.Descending
                            ? ">"
                            : "")}")));
            set => _DefaultSort = value;
        }
        private string _DefaultSort;

        public ObservationConfig Observe {
            get => _Observe ?? (_Observe = new());
            set => _Observe = value;
        }
        private ObservationConfig _Observe;

        public class ObservationConfig {
            public ChunkConfig Add {
                get => _Add ?? (_Add = new());
                set => _Add = value;
            }
            private ChunkConfig _Add;

            public ChunkConfig Remove {
                get => _Remove ?? (_Remove = new());
                set => _Remove = value;
            }
            private ChunkConfig _Remove;
        }

        public class ChunkConfig {
            public int Size { get; set; } = 1000;
            public int Delay { get; set; } = 10;
        }
    }
}
