using Brows.Extensions;
using Domore.Logs;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Brows {
    partial class EntryStreamImageControl {
        private static readonly ILog Log = Logging.For(typeof(EntryStreamImageControl));

        private CancellationTokenSource TokenSource;
        private IEntryStreamGui ConsumingSource;

        private async Task<bool> ConsumeSource(CancellationToken token) {
            if (Log.Info()) {
                Log.Info(nameof(ConsumeSource));
            }
            var stream = EntryStreamGui;
            if (stream?.Source?.StreamValid != true) {
                Image.Source = null;
                return false;
            }
            var lengthMax = stream.Options?.ImageSourceLengthMax;
            if (lengthMax.HasValue && lengthMax.Value < stream.Source.StreamLength) {
                Image.Source = null;
                return false;
            }
            var imageSource = ConsumingSource = stream;
            var image = default(ImageSource);
            try {
                image = await stream.Source.Image(token);
                return true;
            }
            catch (Exception ex) {
                if (ex is OperationCanceledException canceled && canceled.CancellationToken == token) {
                    if (Log.Debug()) {
                        Log.Debug(nameof(OperationCanceledException));
                    }
                }
                else {
                    if (Log.Debug()) {
                        Log.Debug(ex);
                    }
                }
                return false;
            }
            finally {
                if (imageSource == ConsumingSource) {
                    Image.Source = image;
                }
            }
        }

        private async void Image_Loaded(object sender, RoutedEventArgs e) {
            if (Log.Info()) {
                Log.Info(nameof(Image_Loaded));
            }
            await ChangeEntryStreamGui();

        }

        private void Image_Unloaded(object sender, RoutedEventArgs e) {
            if (Log.Info()) {
                Log.Info(nameof(Image_Unloaded));
            }
            try { TokenSource?.Cancel(); }
            catch { }
        }

        private async Task ChangeEntryStreamGui() {
            if (Log.Info()) {
                Log.Info(nameof(ChangeEntryStreamGui));
            }
            try { TokenSource?.Cancel(); }
            catch { }
            using (var tokenSource = TokenSource = new()) {
                var token = tokenSource.Token;
                var stream = EntryStreamGui;
                stream?.State?.Image?.Change(loading: true, success: false);

                var consumed = await ConsumeSource(token);
                TokenSource = null;

                stream?.State?.Image?.Change(loading: false, success: consumed);
            }
        }

        protected override string EntryStreamViewName => "Image";

        protected sealed override async void OnEntryStreamGuiChanged(DependencyPropertyChangedEventArgs e) {
            if (Log.Info()) {
                Log.Info(nameof(OnEntryStreamGuiChanged));
            }
            await ChangeEntryStreamGui();
            base.OnEntryStreamGuiChanged(e);
        }

        public EntryStreamImageControl() {
            InitializeComponent();
        }
    }
}
