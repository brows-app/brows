using System;
using System.Windows;
using TEXTREADER = Domore.Windows.Controls.TextReader;

namespace Brows.Entries;

public sealed class EntryStreamTextControl : EntryStreamGuiControl {
    private TEXTREADER TextReader;
    private bool MarkdownFallback;

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
        var gui = ViewModel;
        var options = gui?.Options;
        var limit = options?.TextSourceLengthMax;
        if (MarkdownFallback && options?.MarkdownSourceLengthMax is long markdownLimit) {
            limit = limit is long textLimit ? Math.Min(textLimit, markdownLimit) : markdownLimit;
        }
        reader.TextReaderOptions = options?.TextDecoderOptions;
        reader.TextReaderSourceLengthMax = limit;
        reader.TextReaderSource = gui is null
            ? null
            : new EntryStreamTextSource(gui.Source, MarkdownFallback ? limit : null);
    }

    internal void ShowMarkdownFallback(IEntryStreamGui gui) {
        MarkdownFallback = true;
        ViewModel = gui;
    }

    protected sealed override void OnViewModelChanged(IEntryStreamGui oldValue, IEntryStreamGui newValue) {
        ChangeEntryStreamGui();
        base.OnViewModelChanged(oldValue, newValue);
    }

    protected sealed override string EntryStreamViewName => "Text";

    public sealed override void OnApplyTemplate() {
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
