using System;

namespace Brows {
    internal sealed class PanelHistoryItem {
        public long CurrentEntryTimestamp =>
            _CurrentEntryTimestamp;
        private long _CurrentEntryTimestamp;

        public string CurrentEntryID {
            get => _CurrentEntryID;
            set {
                _CurrentEntryID = value;
                _CurrentEntryTimestamp = Environment.TickCount64;
            }
        }
        private string _CurrentEntryID;

        public string ID { get; }

        public PanelHistoryItem(string id) {
            ID = id;
        }
    }
}
