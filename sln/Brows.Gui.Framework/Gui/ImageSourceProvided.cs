using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Gui {
    using Logger;

    public abstract class ImageSourceProvided : Image {
        public CancellationToken CancellationToken { get; }

        public ImageSourceProvided(CancellationToken cancellationToken) {
            CancellationToken = cancellationToken;
        }
    }

    public class ImageSourceProvided<TInput> : ImageSourceProvided {
        private static readonly PropertyChangedEventArgs SourceLoadedEventArgs = new(nameof(SourceLoaded));
        private static readonly PropertyChangedEventArgs SourceLoadingEventArgs = new(nameof(SourceLoading));

        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(ImageSourceProvided<TInput>)));
        private ILog _Log;

        private async Task SourceLoad() {
            var provider = Provider;
            if (provider != null) {
                Change(
                    field: ref _Source,
                    value: await provider.GetImageSource(Input, Size, CancellationToken),
                    propertyName: nameof(Source));
            }
        }

        public TInput Input { get; }
        public IImageSourceProvider<TInput> Provider { get; }

        public override ImageSize Size {
            get => _Size;
            set {
                if (_Size.Equals(value) != true) {
                    _Size = value;
                    _Source = null;
                    NotifyPropertyChanged(nameof(Size));
                    NotifyPropertyChanged(nameof(Source));
                }
            }
        }
        private ImageSize _Size;

        public bool SourceLoaded {
            get => _SourceLoaded;
            private set => Change(ref _SourceLoaded, value, SourceLoadedEventArgs);
        }
        private bool _SourceLoaded;

        public bool SourceLoading {
            get => _SourceLoading;
            private set => Change(ref _SourceLoading, value, SourceLoadingEventArgs);
        }
        private bool _SourceLoading;

        public override object Source {
            get {
                if (_Source == null) {
                    if (SourceLoaded == false) {
                        SourceLoaded = true;
                        SourceLoading = true;
                        SourceLoad().ContinueWith(task => {
                            var error = task.Exception;
                            if (error != null) {
                                if (Log.Warn()) {
                                    Log.Warn(error);
                                }
                            }
                            SourceLoading = false;
                        });
                    }
                }
                return _Source;
            }
        }
        private object _Source;

        public ImageSourceProvided(TInput input, IImageSourceProvider<TInput> provider, CancellationToken cancellationToken) : base(cancellationToken) {
            Input = input;
            Provider = provider;
        }
    }
}
