using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Brows.Entries;

public sealed class EntryStreamImageControl : EntryStreamGuiControl {
    private static readonly ILog Log = Logging.For(typeof(EntryStreamImageControl));

    private CancellationTokenSource TokenSource;
    private Image Image;
    private readonly Dictionary<IEntryStreamGuiView, CancellationTokenSource> ImageLoads =
        new(ReferenceEqualityComparer.Instance);

    private async Task<bool> ConsumeSource(CancellationTokenSource tokenSource) {
        if (Log.Info()) {
            Log.Info(nameof(ConsumeSource));
        }
        var token = tokenSource.Token;
        var stream = ViewModel;
        var image = default(ImageSource);
        try {
            if (stream is null) {
                return false;
            }
            var lengthMax = stream.Options?.ImageSourceLengthMax;
            if (lengthMax.HasValue && lengthMax.Value < stream.Source.StreamLength) {
                return false;
            }
            image = await stream.Source.Image(token);
            return image is not null;
        }
        catch (Exception ex) {
            if (ex is OperationCanceledException && token.IsCancellationRequested) {
                if (Log.Debug()) {
                    Log.Debug(nameof(OperationCanceledException));
                }
            }
            else if (Log.Debug()) {
                Log.Debug(ex);
            }
            return false;
        }
        finally {
            if (ReferenceEquals(TokenSource, tokenSource) && Image is not null) {
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
        CancelImageLoad();
    }

    private void CancelImageLoad() {
        var tokenSource = TokenSource;
        TokenSource = null;
        try {
            tokenSource?.Cancel();
        }
        catch {
        }
    }

    private Task ChangeEntryStreamGui() {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CurrentLoadTask = completion.Task;
        _ = CompleteChangeEntryStreamGui(completion);
        return completion.Task;
    }

    private async Task CompleteChangeEntryStreamGui(TaskCompletionSource completion) {
        try {
            await ChangeEntryStreamGuiAsync();
            completion.TrySetResult();
        }
        catch (Exception exception) {
            completion.TrySetException(exception);
        }
    }

    private async Task ChangeEntryStreamGuiAsync() {
        if (Log.Info()) {
            Log.Info(nameof(ChangeEntryStreamGui));
        }
        var image = Image;
        if (image is null) {
            return;
        }
        CancelImageLoad();
        using (var tokenSource = TokenSource = new()) {
            var stream = ViewModel;
            var imageState = stream?.State?.Image;
            if (imageState is not null) {
                ImageLoads[imageState] = tokenSource;
                imageState.Change(loading: true, success: false);
            }

            var consumed = await ConsumeSource(tokenSource);

            /*
             * A newer call may have replaced TokenSource while this one was
             * consuming. Each image state can finish independently, but only
             * its latest load may finish it when multiple GUIs share a state.
             */
            var isCurrent = ReferenceEquals(TokenSource, tokenSource);
            if (isCurrent) {
                TokenSource = null;
            }
            var ownsImageState = imageState is not null &&
                ImageLoads.TryGetValue(imageState, out var imageLoad) &&
                ReferenceEquals(imageLoad, tokenSource);
            if (ownsImageState) {
                ImageLoads.Remove(imageState);
                imageState.Change(loading: false, success: consumed);
            }
        }
    }

    internal Task CurrentLoadTask { get; private set; }

    protected override string EntryStreamViewName => "Image";

    protected sealed override async void OnViewModelChanged(IEntryStreamGui oldValue, IEntryStreamGui newValue) {
        if (Log.Info()) {
            Log.Info(nameof(OnViewModelChanged));
        }
        base.OnViewModelChanged(oldValue, newValue);
        if (Image is not null) {
            await ChangeEntryStreamGui();
        }
    }
    public override void OnApplyTemplate() {
        var oldImage = Image;
        if (oldImage is not null) {
            oldImage.Loaded -= Image_Loaded;
            oldImage.Unloaded -= Image_Unloaded;
            CancelImageLoad();
        }
        base.OnApplyTemplate();
        Image = GetTemplateChild("PART_Image") as Image;
        if (Image is null) {
            return;
        }
        Image.Loaded += Image_Loaded;
        Image.Unloaded += Image_Unloaded;
        if (Image.IsLoaded) {
            _ = ChangeEntryStreamGui();
        }
    }

}
