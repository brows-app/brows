using System.Collections.Generic;

namespace Brows {
    internal sealed class PanelHistory {
        private readonly PanelHistorySet Cache = new();
        private readonly Stack<PanelHistoryItem> History = new();
        private readonly Stack<PanelHistoryItem> Futures = new();

        public bool SettingBack { get; set; }
        public bool SettingForward { get; set; }
        public PanelHistoryItem Current { get; private set; }

        public PanelHistoryItem Set(string id) {
            if (Current != null) {
                if (SettingBack == false && SettingForward == false) {
                    History.Push(Current);
                }
            }
            return Current = Cache.GetOrAdd(id);
        }

        public PanelHistoryItem Back() {
            if (History.Count == 0) {
                return null;
            }
            Futures.Push(Current);
            return Current = History.Pop();
        }

        public PanelHistoryItem Forward() {
            if (Futures.Count == 0) {
                return null;
            }
            History.Push(Current);
            return Current = Futures.Pop();
        }
    }
}
