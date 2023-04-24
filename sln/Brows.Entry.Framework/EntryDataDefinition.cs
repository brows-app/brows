using Domore.Notification;
using System;
using System.Collections;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class EntryDataDefinition : Notifier, IEntryDataDefinition {
        internal static readonly PropertyChangedEventArgs WidthEvent = new(nameof(Width));
        internal static readonly PropertyChangedEventArgs AlignmentEvent = new(nameof(Alignment));
        internal static readonly PropertyChangedEventArgs ConverterEvent = new(nameof(Converter));
        internal static readonly PropertyChangedEventArgs ConverterParameterEvent = new(nameof(ConverterParameter));

        protected EntryDataDefinition(string key) {
            Key = key;
        }

        protected EntryDataDefinition() {
            Key = GetType().Name;
        }

        public abstract IExportResourceKey ResourceKey { get; }

        public double Width {
            get => _Width;
            set => Change(ref _Width, value, WidthEvent);
        }
        private double _Width = double.NaN;

        public EntryDataAlignment Alignment {
            get => _Alignment;
            set => Change(ref _Alignment, value, AlignmentEvent);
        }
        private EntryDataAlignment _Alignment = EntryDataAlignment.Default;

        public IEntryDataConverter Converter {
            get => _Converter;
            set => Change(ref _Converter, value, ConverterEvent);
        }
        private IEntryDataConverter _Converter;

        public object ConverterParameter {
            get => _ConverterParameter;
            set => Change(ref _ConverterParameter, value, ConverterParameterEvent);
        }
        private object _ConverterParameter;

        public virtual string Name =>
            null;

        public virtual string Group =>
            null;

        public string Key { get; }

        public abstract Task<object> GetValue(IEntry entry, Action<object> progress, CancellationToken cancellationToken);
        public abstract int CompareValue(IEntry x, IEntry y);

        public virtual void RefreshValue(IEntry entry) {
        }

        public virtual bool SuggestKey(ICommandContext context) {
            return true;
        }
    }

    public abstract class EntryDataDefinition<TEntry, TValue> : EntryDataDefinition, IExport<TEntry> where TEntry : IEntry {
        private class ComponentResourceKey : IExportResourceKey {
            string IExportResourceKey.For(string key) {
                return $"{typeof(TEntry).Name}_{key}";
            }
        }

        protected EntryDataDefinition(string key) : base(key) {
        }

        protected EntryDataDefinition() {
        }

        protected abstract Task<TValue> GetValue(TEntry entry, Action<TValue> progress, CancellationToken cancellationToken);

        protected virtual void RefreshValue(TEntry entry) {
        }

        public sealed override IExportResourceKey ResourceKey =>
            _ResourceKey ?? (
            _ResourceKey = new ComponentResourceKey());
        private IExportResourceKey _ResourceKey;

        public sealed override async Task<object> GetValue(IEntry entry, Action<object> progress, CancellationToken cancellationToken) {
            if (entry is TEntry tentry) {
                return await GetValue(tentry, value => progress?.Invoke(value), cancellationToken);
            }
            return null;
        }

        public sealed override int CompareValue(IEntry x, IEntry y) {
            return Comparer.Default.Compare(x?[Key]?.Value, y?[Key]?.Value);
        }

        public sealed override void RefreshValue(IEntry entry) {
            if (entry is TEntry tentry) {
                RefreshValue(tentry);
            }
        }
    }
}
