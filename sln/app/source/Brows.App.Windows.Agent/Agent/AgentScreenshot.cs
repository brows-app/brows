using Domore.Logs;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Brows.Agent;

/*
 * Renders a window's content to a PNG file. WPF renders the visual tree itself, so the window does not need to
 * be visible on screen, but content hosted in native windows is not drawn.
 */
internal static class AgentScreenshot {
    private static readonly ILog Log = Logging.For(typeof(AgentScreenshot));

    private static FrameworkElement Root(Window window) {
        var root = VisualTreeHelper.GetChildrenCount(window) > 0
            ? VisualTreeHelper.GetChild(window, 0) as FrameworkElement
            : null;
        if (root is null) {
            throw new InvalidOperationException("The window has no content to render.");
        }
        return root;
    }

    private static (byte[] Png, bool Empty) Capture(Window window) {
        window.UpdateLayout();
        var root = Root(window);
        var dpi = VisualTreeHelper.GetDpi(window);
        var width = (int)Math.Ceiling(root.ActualWidth * dpi.DpiScaleX);
        var height = (int)Math.Ceiling(root.ActualHeight * dpi.DpiScaleY);
        var hasSize = width > 0 && height > 0;
        if (!hasSize) {
            throw new InvalidOperationException("The window has no size to render.");
        }
        var bitmap = new RenderTargetBitmap(
            width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var context = background.RenderOpen()) {
            context.DrawRectangle(window.Background, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
        }
        bitmap.Render(background);
        bitmap.Render(root);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = new MemoryStream()) {
            encoder.Save(stream);
            return (stream.ToArray(), pixels.All(value => value == 0));
        }
    }

    private static async Task Publish(string path, byte[] png, Func<string, byte[], CancellationToken, Task> write,
                                      CancellationToken token) {
        var directory = Path.GetDirectoryName(path);
        Directory.CreateDirectory(directory);
        token.ThrowIfCancellationRequested();
        var temporary = Path.Combine(directory, $".brows-shot-{Guid.NewGuid():N}.tmp");
        try {
            await write(temporary, png, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        catch {
            try {
                File.Delete(temporary);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                if (Log.Warn()) {
                    Log.Warn(exception);
                }
            }
            throw;
        }
    }

    internal static string ValidPath(string path) {
        var pathIsValid =
            !string.IsNullOrWhiteSpace(path) &&
            Path.IsPathFullyQualified(path) &&
            string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase);
        if (!pathIsValid) {
            throw new ArgumentException("Specify an absolute path to a .png file.");
        }
        return Path.GetFullPath(path);
    }

    /*
     * Capture and encode on the window's dispatcher, then publish the bytes on a worker.
     * Returns true when every pixel is transparent.
     */
    internal static async Task<bool> Save(Window window, string path,
                                          Func<string, byte[], CancellationToken, Task> write,
                                          CancellationToken token) {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(window);
        var file = ValidPath(path);
        var (png, empty) = Capture(window);
        var publish = Task.Run(() => Publish(file, png, write ?? File.WriteAllBytesAsync, token), token);
        try {
            await publish.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            /*
             * The request can finish while the destination is stalled. Observe the worker's eventual failure
             * and let it clean up its temporary file when the filesystem responds.
             */
            _ = publish.ContinueWith(
                task => { _ = task.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            throw;
        }
        return empty;
    }
}
