using Brows.Windows.Controls;
using Domore.Buffers;
using Domore.IO.Extensions;
using Domore.Logs;
using Domore.Text;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Brows.Entries;

/// <summary>
/// Displays an entry stream as rendered Markdown.
/// </summary>
public sealed class EntryStreamMarkdownControl : EntryStreamGuiControl {
    /*
     * Keep these limits and the pipeline aligned with the renderer in Brows.MD.Windows 0.1.0.
     */
    private const int MaximumSourceLength = 250_000;
    private const int MaximumBlocks = 2_000;
    private const int MaximumSyntaxNodes = 8_000;

    private static readonly ILog Log = Logging.For(typeof(EntryStreamMarkdownControl));
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseTaskLists()
        .UseAutoLinks()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .DisableHtml()
        .Build();

    private CancellationTokenSource LoadCancellation;
    private MarkdownControl MarkdownViewer;
    private EntryStreamTextControl TextFallback;

    private static bool ExceedsRendererLimit(string markdown) {
        if (markdown.Length > MaximumSourceLength) {
            return true;
        }

        MarkdownDocument syntax;
        try {
            syntax = Markdig.Markdown.Parse(markdown, Pipeline);
        }
        catch (ArgumentException) {
            /*
             * The renderer displays the source when Markdig rejects excessive nesting.
             */
            return false;
        }
        return syntax.Descendants<Block>().Skip(MaximumBlocks).Any() ||
            syntax.Descendants().Skip(MaximumSyntaxNodes).Any();
    }

    private static BufferOptions CopyBufferOptions(BufferOptions options) {
        return new BufferOptions {
            Size = options.Size,
            Clear = options.Clear,
            Shared = options.Shared
        };
    }

    private static async Task<(string Markdown, bool FallbackToText)> ReadMarkdown(
        IEntryStreamGui gui, CancellationToken token) {
        var source = gui.Source;
        var options = gui.Options;
        var readLimit = options?.MarkdownSourceLengthMax;
        var exceedsLimit = readLimit is long limit && source.StreamLength > limit;
        if (exceedsLimit) {
            return default;
        }

        var settings = options?.MarkdownDecoderOptions;
        var decoder = settings is null ? new DecodedTextOptions() : new DecodedTextOptions {
            Encoding = new List<string>(settings.Encoding),
            EncodingFallback = new Dictionary<string, string>(
                settings.EncodingFallback, settings.EncodingFallback.Comparer),
            StreamBuffer = CopyBufferOptions(settings.StreamBuffer),
            TextBuffer = CopyBufferOptions(settings.TextBuffer)
        };
        using var buffers = decoder.Disposable();
        var decoded = await new EntryStreamTextSource(source, readLimit)
            .DecodeText(default(DecodedTextDelegate), decoder, token).ConfigureAwait(false);
        if (decoded?.Success != true) {
            return default;
        }

        token.ThrowIfCancellationRequested();
        var markdown = decoded.Text();
        if (!ExceedsRendererLimit(markdown)) {
            return (markdown, false);
        }

        var textLimit = options?.TextSourceLengthMax;
        var fallbackToText = textLimit is null || (textLimit >= 0 && source.StreamLength <= textLimit);
        return (null, fallbackToText);
    }

    private void ClearTextFallback() {
        var gui = TextFallback?.ViewModel;
        if (gui is not null) {
            TextFallback.ViewModel = null;
            gui.State?.Text?.Change(loading: false, success: false);
        }
    }

    private void CancelLoad(IEntryStreamGui gui) {
        var previous = LoadCancellation;
        if (previous is null) {
            return;
        }
        LoadCancellation = null;
        previous.Cancel();
        gui?.State?.Markdown?.Change(loading: false, success: false);
    }

    private async void LoadMarkdown() {
        var gui = ViewModel;
        var viewer = MarkdownViewer;
        if (viewer is null || LoadCancellation is not null) {
            return;
        }
        ClearTextFallback();
        viewer.Markdown = null;
        viewer.Visibility = Visibility.Visible;
        if (gui is null || !IsLoaded) {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        LoadCancellation = cancellation;
        var state = gui.State?.Markdown;
        state?.Change(loading: true, success: false);

        (string Markdown, bool FallbackToText) result = default;
        try {
            var token = cancellation.Token;
            result = await Task.Run(() => ReadMarkdown(gui, token), token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
        }
        catch (Exception ex) {
            if (Log.Debug()) {
                Log.Debug(ex);
            }
        }

        if (ReferenceEquals(LoadCancellation, cancellation)) {
            LoadCancellation = null;
            viewer.Markdown = result.Markdown;
            if (result.FallbackToText && TextFallback is not null) {
                viewer.Visibility = Visibility.Collapsed;
                TextFallback.ShowMarkdownFallback(gui);
            }
            state?.Change(loading: false, success: result.Markdown is not null);
        }
    }

    protected sealed override string EntryStreamViewName => "Markdown";

    protected sealed override void OnEntryStreamViewChanged(DependencyPropertyChangedEventArgs e) {
        base.OnEntryStreamViewChanged(e);
        if (e.NewValue is "Text") {
            Visibility = Visibility.Visible;
        }
    }

    protected sealed override void OnViewModelChanged(IEntryStreamGui oldValue, IEntryStreamGui newValue) {
        CancelLoad(oldValue);
        base.OnViewModelChanged(oldValue, newValue);
        LoadMarkdown();
    }

    /// <summary>
    /// Initializes an entry Markdown preview control.
    /// </summary>
    public EntryStreamMarkdownControl() {
        Loaded += (_, _) => LoadMarkdown();
        Unloaded += (_, _) => {
            CancelLoad(ViewModel);
            ClearTextFallback();
        };
    }

    /// <summary>
    /// Initializes the Markdown viewer when the control template changes.
    /// </summary>
    public sealed override void OnApplyTemplate() {
        CancelLoad(ViewModel);
        ClearTextFallback();
        base.OnApplyTemplate();
        MarkdownViewer = GetTemplateChild("PART_MarkdownControl") as MarkdownControl;
        TextFallback = GetTemplateChild("PART_TextFallback") as EntryStreamTextControl;
        LoadMarkdown();
    }
}
