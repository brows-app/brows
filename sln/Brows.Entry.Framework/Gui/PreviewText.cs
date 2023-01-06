using Domore.Logs;
using Domore.Notification;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Gui {
    internal class PreviewText : Notifier, IPreviewText {
        private static readonly ILog Log = Logging.For(typeof(PreviewText));

        private async Task ContentsLoad() {
            if (Log.Info()) {
                Log.Info(nameof(ContentsLoad) + " > " + Input?.ID);
            }
            var provider = Provider;
            if (provider != null) {
                var text = provider.GetPreviewText(Input, CancellationToken);
                await foreach (var str in text) {
                    if (Stopping) {
                        if (Log.Info()) {
                            Log.Info(nameof(Stopping) + " > " + Input?.ID);
                        }
                        break;
                    }
                    Contents = str;
                }
            }
        }

        public IPreviewInput Input { get; }
        public IPreviewProvider Provider { get; }
        public CancellationToken CancellationToken { get; }

        public bool ContentsLoaded {
            get => _ContentsLoaded;
            private set => Change(ref _ContentsLoaded, value, nameof(ContentsLoaded));
        }
        private bool _ContentsLoaded;

        public bool ContentsLoading {
            get => _ContentsLoading;
            private set => Change(ref _ContentsLoading, value, nameof(ContentsLoading));
        }
        private bool _ContentsLoading;

        public bool Stopping {
            get => _Stopping;
            private set => Change(ref _Stopping, value, nameof(Stopping));
        }
        private bool _Stopping;

        public string Contents {
            get {
                if (_Contents == "") {
                    if (ContentsLoaded == false) {
                        ContentsLoaded = true;
                        ContentsLoading = true;
                        ContentsLoad().ContinueWith(task => {
                            var error = task.Exception;
                            if (error != null) {
                                if (Log.Warn()) {
                                    Log.Warn(error);
                                }
                            }
                            ContentsLoading = false;
                        });
                    }
                }
                return _Contents;
            }
            private set {
                Change(ref _Contents, value, nameof(Contents));
            }
        }
        private string _Contents = "";

        public PreviewText(IPreviewInput input, IPreviewProvider provider, CancellationToken cancellationToken) {
            Input = input;
            Provider = provider;
            CancellationToken = cancellationToken;
        }

        public void Stop() {
            if (Log.Info()) {
                Log.Info(
                    nameof(Stop),
                    $"{nameof(Input.ID)} > {Input?.ID}");
            }
            Stopping = true;
        }
    }
}
