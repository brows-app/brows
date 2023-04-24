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

        private async Task<bool> ConsumeSource(IEntryStreamGui gui, CancellationToken token) {
            if (Log.Info()) {
                Log.Info(nameof(ConsumeSource));
            }
            if (gui?.Source?.StreamValid != true) {
                Image.Source = null;
                return false;
            }
            var lengthMax = gui.Options?.ImageSourceLengthMax;
            if (lengthMax.HasValue && lengthMax.Value < gui.Source.StreamLength) {
                Image.Source = null;
                return false;
            }
            var imageSource = ConsumingSource = gui;
            var image = default(ImageSource);
            try {
                image = await gui.Source.Image(token);
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

        private void This_Unloaded(object sender, RoutedEventArgs e) {
            if (Log.Info()) {
                Log.Info(nameof(This_Unloaded));
            }
            try { TokenSource?.Cancel(); }
            catch { }
        }

        protected override async void OnEntryStreamGuiChanged(DependencyPropertyChangedEventArgs e) {
            if (Log.Info()) {
                Log.Info(nameof(OnEntryStreamGuiChanged));
            }
            try { TokenSource?.Cancel(); }
            catch { }
            using (var tokenSource = TokenSource = new()) {
                var token = tokenSource.Token;
                var source = e.NewValue as IEntryStreamGui;
                source?.State?.Image?.Change(loading: true, success: false);

                var consumed = await ConsumeSource(source, token);
                TokenSource = null;

                source?.State?.Image?.Change(loading: false, success: consumed);
            }
            base.OnEntryStreamGuiChanged(e);
        }

        public EntryStreamImageControl() {
            Unloaded += This_Unloaded;
            InitializeComponent();
        }
    }
}
