using Domore.IO;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using TEXTREADER = Domore.Windows.Controls.TextReader;

namespace Brows.Entries;

public sealed class EntryStreamTextControl : EntryStreamGuiControl {
    private TEXTREADER TextReader;

    private void TextReader_Loaded(object sender, RoutedEventArgs e) {
        ChangeEntryStreamGui();
    }

    private void TextReader_TextReaderLoadingChanged(object sender, RoutedEventArgs e) {
        if (sender is TEXTREADER control) {
            ViewModel?.State?.Text?.Change(loading: control.TextReaderLoading);
        }
    }

    private void TextReader_TextReaderSuccessChanged(object sender, RoutedEventArgs e) {
        if (sender is TEXTREADER control) {
            ViewModel?.State?.Text?.Change(success: control.TextReaderSuccess);
        }
    }

    private void ChangeEntryStreamGui() {
        var reader = TextReader;
        if (reader is null) {
            return;
        }
        reader.TextReaderOptions = ViewModel?.Options?.TextDecoderOptions;
        reader.TextReaderSourceLengthMax = ViewModel?.Options?.TextSourceLengthMax;
        reader.TextReaderSource = ViewModel is null
            ? null
            : new StreamText(ViewModel.Source);
    }

    private sealed class StreamText : IStreamText {
        public IEntryStreamSource Source { get; }

        public StreamText(IEntryStreamSource source) {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public async Task<IDisposable> StreamReady(CancellationToken token) {
            return await Source.StreamReady(token);
        }

        public Task<long> StreamLength(CancellationToken token) {
            return Task.FromResult(Source.StreamLength);
        }

        Stream IStreamText.StreamText() {
            return Source.Stream;
        }
    }

    protected sealed override void OnViewModelChanged(IEntryStreamGui oldValue, IEntryStreamGui newValue) {
        ChangeEntryStreamGui();
        base.OnViewModelChanged(oldValue, newValue);
    }

    protected override string EntryStreamViewName => "Text";

    public override void OnApplyTemplate() {
        var oldReader = TextReader;
        if (oldReader is not null) {
            oldReader.Loaded -= TextReader_Loaded;
            oldReader.TextReaderLoadingChanged -= TextReader_TextReaderLoadingChanged;
            oldReader.TextReaderSuccessChanged -= TextReader_TextReaderSuccessChanged;
        }
        base.OnApplyTemplate();
        TextReader = GetTemplateChild("PART_TextReader") as TEXTREADER;
        if (TextReader is null) {
            return;
        }
        TextReader.Loaded += TextReader_Loaded;
        TextReader.TextReaderLoadingChanged += TextReader_TextReaderLoadingChanged;
        TextReader.TextReaderSuccessChanged += TextReader_TextReaderSuccessChanged;
        if (TextReader.IsLoaded) {
            ChangeEntryStreamGui();
        }
    }
}