using System;
using System.Collections.Generic;

namespace Brows {
    internal class PanelHistory {
        private readonly PanelHistorySet Cache = new PanelHistorySet();
        private readonly Stack<PanelHistoryItem> History = new Stack<PanelHistoryItem>();
        private readonly Stack<PanelHistoryItem> Futures = new Stack<PanelHistoryItem>();

        public bool SettingBack { get; set; }
        public bool SettingForward { get; set; }
        public PanelHistoryItem Current { get; private set; }

        public PanelHistoryItem Set(IPanelID id) {
            if (null == id) throw new ArgumentNullException(nameof(id));
            if (Current != null) {
                if (SettingBack == false && SettingForward == false) {
                    History.Push(Current);
                }
            }
            return Current = Cache.GetOrAdd(id.Value, () => new PanelHistoryItem(id));
        }

        public PanelHistoryItem Back() {
            if (History.Count == 0) return null;
            Futures.Push(Current);
            return Current = History.Pop();
        }

        public PanelHistoryItem Forward() {
            if (Futures.Count == 0) return null;
            History.Push(Current);
            return Current = Futures.Pop();
        }
    }
}
