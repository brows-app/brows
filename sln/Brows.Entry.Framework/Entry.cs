using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using ComponentModel;
    using Gui;
    using Threading.Tasks;

    public abstract class Entry : NotifyPropertyChanged, IEntry {
        private static readonly PropertyChangedEventArgs IconEventArgs = new(nameof(Icon));
        private static readonly PropertyChangedEventArgs OverlayEventArgs = new(nameof(Overlay));
        private static readonly PropertyChangedEventArgs ThumbnailEventArgs = new(nameof(Thumbnail));
        private static readonly PropertyChangedEventArgs PreviewImageEventArgs = new(nameof(PreviewImage));
        private static readonly PropertyChangedEventArgs PreviewTextEventArgs = new(nameof(PreviewText));
        private static readonly PropertyChangedEventArgs SelectedEventArgs = new(nameof(Selected));

        private IEntryBrowser Browser;

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<Entry>());
        private TaskHandler _TaskHandler;

        protected abstract IReadOnlySet<string> Keys { get; }

        protected abstract IIconProvider IconProvider { get; }
        protected abstract IOverlayProvider OverlayProvider { get; }
        protected abstract IPreviewProvider PreviewProvider { get; }
        protected abstract IThumbnailProvider ThumbnailProvider { get; }

        protected virtual IconInput IconInput =>
            _IconInput ?? (
            _IconInput = new IconInput(IconStock.Unknown, ID));
        private IconInput _IconInput;

        protected virtual OverlayInput OverlayInput =>
            _OverlayInput ?? (
            _OverlayInput = new OverlayInput(ID));
        private OverlayInput _OverlayInput;

        protected virtual PreviewInput PreviewInput =>
            _PreviewInput ?? (
            _PreviewInput = new PreviewInput(ID, File));
        private PreviewInput _PreviewInput;

        protected virtual ThumbnailInput ThumbnailInput =>
            _ThumbnailInput ?? (
            _ThumbnailInput = new ThumbnailInput(ID));
        private ThumbnailInput _ThumbnailInput;

        protected CancellationToken CancellationToken { get; }

        protected Entry(CancellationToken cancellationToken) {
            CancellationToken = cancellationToken;
        }

        protected async Task<bool> Browse(string id, CancellationToken cancellationToken) {
            var browser = Browser;
            if (browser != null) {
                return await browser.Browse(id, cancellationToken);
            }
            return false;
        }

        protected virtual Task<bool> Open(CancellationToken cancellationToken) {
            return Browse(ID, cancellationToken);
        }

        protected abstract IEntryData Get(string key);

        public static string ThumbnailKey => ThumbnailData.Key;
        public static IEntryData ThumbnailData { get; } = new EntryThumbnailData();

        public string Rename {
            get => _Rename;
            private set => Change(ref _Rename, value, nameof(Rename));
        }
        private string _Rename;

        public IEntryData this[string key] {
            get {
                return Get(key);
            }
        }

        public abstract string ID { get; }
        public virtual string File => null;
        public virtual string Name => ID;

        public Image Icon {
            get => _Icon ?? (_Icon = new ImageSourceProvided<IIconInput>(IconInput, IconProvider, CancellationToken));
            private set => Change(ref _Icon, value, IconEventArgs);
        }
        private Image _Icon;

        public Image Overlay {
            get => _Overlay ?? (_Overlay = new ImageSourceProvided<IOverlayInput>(OverlayInput, OverlayProvider, CancellationToken));
            private set => Change(ref _Overlay, value, OverlayEventArgs);
        }
        private Image _Overlay;

        public Image Thumbnail {
            get => _Thumbnail ?? (_Thumbnail = new ImageSourceProvided<IThumbnailInput>(ThumbnailInput, ThumbnailProvider, CancellationToken) { Size = new ImageSize(100, 100) });
            private set => Change(ref _Thumbnail, value, ThumbnailEventArgs);
        }
        private Image _Thumbnail;

        public Image PreviewImage {
            get => _PreviewImage ?? (_PreviewImage = new ImageSourceProvided<IPreviewInput>(PreviewInput, PreviewProvider, CancellationToken) { Size = new ImageSize(500, 500) });
            private set => Change(ref _PreviewImage, value, PreviewImageEventArgs);
        }
        private Image _PreviewImage;

        public IPreviewText PreviewText {
            get => _PreviewText ?? (_PreviewText = new PreviewText(PreviewInput, PreviewProvider, CancellationToken));
            private set => Change(ref _PreviewText, value, PreviewTextEventArgs);
        }
        private IPreviewText _PreviewText;

        public bool Selected {
            get => _Selected;
            set => Change(ref _Selected, value, SelectedEventArgs);
        }
        private bool _Selected;

        public object OpenCommand => Request.Create(
            owner: this,
            execute: _ => Open(),
            canExecute: _ => true);

        public void Open() {
            var tokn = CancellationToken;
            var task = Open(tokn);
            TaskHandler.Begin(task);
        }

        public virtual void Refresh(params string[] keys) {
            foreach (var key in Keys) {
                if (keys.Contains(key)) {
                    this[key].Refresh();
                }
            }
        }

        public virtual void Refresh(EntryRefresh flags) {
            if (flags.HasFlag(EntryRefresh.Data)) {
                foreach (var key in Keys) {
                    this[key].Refresh();
                }
            }
            if (flags.HasFlag(EntryRefresh.Icon)) {
                Icon = null;
            }
            if (flags.HasFlag(EntryRefresh.PreviewImage)) {
                PreviewImage = null;
            }
            if (flags.HasFlag(EntryRefresh.PreviewText)) {
                PreviewText = null;
            }
            if (flags.HasFlag(EntryRefresh.Thumbnail)) {
                Thumbnail = null;
            }
            if (flags.HasFlag(EntryRefresh.Overlay)) {
                Overlay = null;
            }
        }

        public void Notify(bool state) {
            NotifyState = state;
        }

        public override string ToString() {
            return ID;
        }

        void IEntry.Begin(IEntryBrowser browser) {
            Browser = browser;
        }

        void IEntry.End() {
            Browser = null;
        }

        void IEntry.Rename(string value) {
            Rename = value;
        }

        string IEntry.Rename() {
            var
            rename = Rename;
            Rename = null;
            return rename;
        }
    }
}
