using System;

namespace Brows {
    using ComponentModel;
    using Gui;

    public class Preview : NotifyPropertyChanged, IControlled<IPreviewController> {
        private void Control_SizeChanged(object sender, EventArgs e) {
        }

        public IEntry Entry {
            get => _Entry;
            private set => Change(ref _Entry, value, nameof(Entry));
        }
        private IEntry _Entry;

        public IPreviewController Controller {
            get => _Controller;
            set {
                var oldValue = _Controller;
                var newValue = value;
                if (Change(ref _Controller, newValue, nameof(Controller))) {
                    if (oldValue != null) {
                        oldValue.SizeChanged -= Control_SizeChanged;
                    }
                    if (newValue != null) {
                        newValue.SizeChanged += Control_SizeChanged;
                    }
                }
            }
        }
        private IPreviewController _Controller;

        public Preview(IEntry entry = null) {
            Entry = entry;
        }

        public void Set(IEntry entry) {
            if (Entry != entry) {
                Entry?.Notify(false);
                Entry?.PreviewText?.Stop();
                Entry?.Refresh(EntryRefresh.PreviewImage | EntryRefresh.PreviewText);
                Entry?.Notify(true);
                Entry = entry;
            }
        }
    }
}
