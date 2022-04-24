using System;
using System.Threading;

namespace Brows {
    internal class PanelHistoryItem : IPanelID {
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

        public string ID =>
            PanelID.Value;

        public IPanelID PanelID { get; }

        public PanelHistoryItem(IPanelID panelID) {
            PanelID = panelID ?? throw new ArgumentNullException(nameof(panelID));
        }

        event EventHandler IPanelID.ValueChanged {
            add => PanelID.ValueChanged += value;
            remove => PanelID.ValueChanged -= value;
        }

        string IPanelID.Value =>
            PanelID.Value;

        void IPanelID.Begin(CancellationToken cancellationToken) {
            PanelID.Begin(cancellationToken);
        }
    }
}
