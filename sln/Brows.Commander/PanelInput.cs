using Domore.Notification;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace Brows {
    internal sealed class PanelInput : Notifier {
        private static readonly PropertyChangedEventArgs TimeoutEvent = new(nameof(Timeout));
        private static readonly PropertyChangedEventArgs AggregateEvent = new(nameof(Aggregate));

        private readonly Stopwatch Stopwatch = new Stopwatch();

        public TimeSpan Timeout {
            get => _Timeout;
            set => Change(ref _Timeout, value, TimeoutEvent);
        }
        private TimeSpan _Timeout = TimeSpan.FromSeconds(1);

        public string Aggregate {
            get => _Aggregate ?? (_Aggregate = "");
            private set => Change(ref _Aggregate, value, AggregateEvent);
        }
        private string _Aggregate;

        public void Clear() {
            Aggregate = null;
            Stopwatch.Reset();
        }

        public IEnumerable<IEntry> Text(string s, IEnumerable<IEntry> entries) {
            if (null == entries) throw new ArgumentNullException(nameof(entries));
            if (null == s) {
                Aggregate = null;
                yield break;
            }
            if (Stopwatch.Elapsed > Timeout) {
                Aggregate = "";
            }
            var text = (Aggregate + s)?.Trim() ?? "";
            if (text == "") {
                Aggregate = null;
                yield break;
            }
            Aggregate = text;
            Stopwatch.Restart();
            foreach (var entry in entries) {
                if (entry.Name.StartsWith(text, StringComparison.CurrentCultureIgnoreCase)) {
                    yield return entry;
                }
            }
            foreach (var entry in entries) {
                if (entry.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)) {
                    yield return entry;
                }
            }
        }
    }
}
