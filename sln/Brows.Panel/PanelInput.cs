using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Brows {
    internal class PanelInput : Notifier {
        private readonly Stopwatch Stopwatch = new Stopwatch();

        public TimeSpan Timeout {
            get => _Timeout;
            set => Change(ref _Timeout, value, nameof(Timeout));
        }
        private TimeSpan _Timeout = TimeSpan.FromSeconds(1);

        public string Text {
            get => _Text ?? (_Text = "");
            private set => Change(ref _Text, value, nameof(Text));
        }
        private string _Text;

        public IEntry Match {
            get => _Match;
            private set => Change(ref _Match, value, nameof(Match));
        }
        private IEntry _Match;

        public void Clear() {
            Text = null;
            Match = null;
            Stopwatch.Reset();
        }

        public IEntry Add(string value, IEnumerable<IEntry> entries) {
            if (null == entries) throw new ArgumentNullException(nameof(entries));
            if (value == null) {
                Text = null;
                return null;
            }
            if (Stopwatch.Elapsed > Timeout) {
                Text = "";
            }
            var text = (Text + value) ?? "";
            if (text.Trim() == "") {
                Text = null;
                return null;
            }
            Text = text;
            Stopwatch.Restart();

            foreach (var entry in entries) {
                if (entry.Name.StartsWith(text, StringComparison.CurrentCultureIgnoreCase)) {
                    return Match = entry;
                }
            }
            foreach (var entry in entries) {
                if (entry.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)) {
                    return Match = entry;
                }
            }
            return null;
        }
    }
}
