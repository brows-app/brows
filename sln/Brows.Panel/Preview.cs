using Domore.Notification;

namespace Brows {
    using Config;
    using Gui;
    using System.Threading;
    using System.Threading.Tasks;

    public class Preview : Notifier, IControlled<IPreviewController> {
        private static IConfig<PreviewConfig> Config =>
            _Config ?? (
            _Config = Configure.File<PreviewConfig>());
        private static IConfig<PreviewConfig> _Config;

        private void Entry_Refreshed(object sender, EntryRefreshedEventArgs e) {
            var entry = sender as IEntry;
            if (entry != null) {
                if (entry == Entry) {
                    Controller?.Refresh();
                }
                else {
                    entry.Refreshed -= Entry_Refreshed;
                }
            }
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
                        oldValue.Config = null;
                    }
                    if (newValue != null) {
                        newValue.Config = () => Config.Loaded;
                    }
                }
            }
        }
        private IPreviewController _Controller;

        public Preview(IEntry entry = null) {
            Set(entry);
        }

        public static async Task Init(CancellationToken cancellationToken) {
            await Config.Load(cancellationToken);
        }

        public void Set(IEntry entry) {
            if (Entry != entry) {
                var oldEntry = Entry;
                var newEntry = Entry = entry;
                if (newEntry != null) {
                    newEntry.Refreshed += Entry_Refreshed;
                }
                if (oldEntry != null) {
                    oldEntry.Refreshed -= Entry_Refreshed;
                    oldEntry.PreviewText?.Stop();
                }
            }
        }
    }
}
