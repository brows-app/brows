using Domore.Logs;
using Domore.Notification;
using System;
using System.ComponentModel;
using System.Threading;

namespace Brows {
    public abstract class Entry : Notifier, IEntry {
        private static readonly ILog Log = Logging.For(typeof(Entry));
        private static readonly PropertyChangedEventArgs SelectEventArgs = new(nameof(Select));

        private event EntrySelectedEventHandler SelectedEvent;
        private event EntryRefreshedEventHandler RefreshedEvent;

        private EntryDataInstance DataInstance =>
            _DataInstance ?? (
            _DataInstance = new EntryDataInstance(this, Provider.Data.Definition, Provider.Token));
        private EntryDataInstance _DataInstance;

        protected CancellationToken Token =>
            Provider.Token;

        protected Provider Provider { get; }

        protected Entry(Provider provider) {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        protected virtual void Selected(bool select) {
        }

        protected virtual void Refreshed() {
        }

        public IEntryData this[string key] =>
            DataInstance[key];

        public abstract string ID { get; }
        public virtual string Name => ID;

        public bool Select {
            get => _Select;
            set {
                if (Change(ref _Select, value, SelectEventArgs)) {
                    Selected(value);
                    SelectedEvent?.Invoke(this, new EntrySelectedEventArgs(value));
                }
            }
        }
        private bool _Select;

        public void Refresh() {
            DataInstance.Refresh();
            Refreshed();
            RefreshedEvent?.Invoke(this, new EntryRefreshedEventArgs());
        }

        public override string ToString() {
            return ID;
        }

        event EntrySelectedEventHandler IEntry.Selected {
            add => SelectedEvent += value;
            remove => SelectedEvent -= value;
        }

        event EntryRefreshedEventHandler IEntry.Refreshed {
            add => RefreshedEvent += value;
            remove => RefreshedEvent -= value;
        }
    }

    public abstract class Entry<TProvider> : Entry where TProvider : Provider {
        protected new TProvider Provider { get; }

        protected Entry(TProvider provider) : base(provider) {
            Provider = provider;
        }
    }
}
