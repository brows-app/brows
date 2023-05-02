using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Brows {
    internal sealed class PanelHistory {
        private static readonly ILog Log = Logging.For(typeof(PanelHistory));

        private readonly PanelHistorySet Cache = new();
        private readonly Stack<PanelHistoryItem> History = new();
        private readonly Stack<PanelHistoryItem> Futures = new();

        private bool CurrentLoaded;
        private bool CurrentLoading;

        public bool SettingBack { get; set; }
        public bool SettingForward { get; set; }
        public PanelHistoryItem Current { get; private set; }

        public PanelHistoryItem Set(string id) {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(Set), id));
            }
            if (Current != null) {
                if (SettingBack == false && SettingForward == false) {
                    History.Push(Current);
                }
            }
            CurrentLoaded = false;
            return Current = Cache.GetOrAdd(id);
        }

        public PanelHistoryItem Back() {
            if (Log.Info()) {
                Log.Info(nameof(Back));
            }
            if (History.Count == 0) {
                return null;
            }
            Futures.Push(Current);
            return Current = History.Pop();
        }

        public PanelHistoryItem Forward() {
            if (Log.Info()) {
                Log.Info(nameof(Forward));
            }
            if (Futures.Count == 0) {
                return null;
            }
            History.Push(Current);
            return Current = Futures.Pop();
        }

        public async void CurrentLoadOpportunity(IProvider provider, Action loaded) {
            if (Log.Debug()) {
                Log.Debug(Log.Join(nameof(CurrentLoadOpportunity), provider?.ID));
            }
            if (CurrentLoaded) {
                return;
            }
            if (CurrentLoading) {
                return;
            }
            if (provider is null) {
                return;
            }
            var entries = provider.Observation;
            if (entries == null) return;
            if (entries.ManualInteraction) {
                return;
            }
            var current = Current;
            if (current == null) {
                return;
            }
            var currentEntryID = current.CurrentEntryID;
            if (currentEntryID == null) {
                return;
            }
            var currentEntry = entries.LookupID(currentEntryID);
            if (currentEntry == null) {
                return;
            }
            CurrentLoading = true;
            try {
                CurrentLoaded = await entries.Current(currentEntry, CancellationToken.None);
            }
            finally {
                CurrentLoading = false;
            }
            if (CurrentLoaded) {
                if (loaded != null) {
                    loaded();
                }
            }
        }
    }
}
