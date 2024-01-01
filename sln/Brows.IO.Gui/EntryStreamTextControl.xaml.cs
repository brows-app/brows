using Domore.IO;
using Domore.Windows.Controls;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Brows {
    partial class EntryStreamTextControl {
        private void PreviewTextControl_Loaded(object sender, RoutedEventArgs e) {
            ChangeEntryStreamGui();
        }

        private void PreviewTextControl_PreviewTextLoadingChanged(object sender, RoutedEventArgs e) {
            var control = sender as PreviewTextControl;
            if (control != null) {
                EntryStreamGui?.State?.Text?.Change(loading: control.PreviewTextLoading);
            }
        }

        private void PreviewTextControl_PreviewTextSuccessChanged(object sender, RoutedEventArgs e) {
            var control = sender as PreviewTextControl;
            if (control != null) {
                EntryStreamGui?.State?.Text?.Change(success: control.PreviewTextSuccess);
            }
        }

        private void ChangeEntryStreamGui() {
            PreviewTextControl.PreviewTextOptions = EntryStreamGui?.Options?.TextDecoderOptions;
            PreviewTextControl.PreviewTextSourceLengthMax = EntryStreamGui?.Options?.TextSourceLengthMax;
            PreviewTextControl.PreviewTextSource = EntryStreamGui == null
                ? null :
                new StreamText(EntryStreamGui.Source);
        }

        protected override string EntryStreamViewName => "Text";

        protected sealed override void OnEntryStreamGuiChanged(DependencyPropertyChangedEventArgs e) {
            ChangeEntryStreamGui();
            base.OnEntryStreamGuiChanged(e);
        }

        public EntryStreamTextControl() {
            InitializeComponent();
        }

        private sealed class StreamText : IStreamText {
            public bool StreamValid => Source.StreamValid;
            public long StreamLength => Source.StreamLength;

            public IEntryStreamSource Source { get; }

            public StreamText(IEntryStreamSource source) {
                Source = source ?? throw new ArgumentNullException(nameof(source));
            }

            public async Task<IDisposable> StreamReady(CancellationToken token) {
                return await Source.StreamReady(token);
            }

            Stream IStreamText.StreamText() {
                return Source.Stream();
            }
        }
    }
}
