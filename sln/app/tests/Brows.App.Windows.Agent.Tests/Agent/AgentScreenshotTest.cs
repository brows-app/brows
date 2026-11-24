using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Brows.Agent;

[TestFixture]
[NonParallelizable]
internal sealed class AgentScreenshotTest {
    private static Window ShowWindow(UIElement content) {
        var window = new Window {
            Background = Brushes.White,
            Content = content,
            Height = 100,
            Left = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
            Top = 0,
            Width = 160
        };
        window.Show();
        window.UpdateLayout();
        DispatcherTools.Pump(window.Dispatcher);
        return window;
    }

    [Test]
    public void Save_WritesThePixelsOfTheWindow() {
        var directory = Directory.CreateTempSubdirectory("BrowsAgentScreenshotTest-");
        try {
            var path = Path.Combine(directory.FullName, "shots", "window.png");
            DispatcherTools.Run(_ => {
                var window = ShowWindow(new Border { Background = Brushes.Red });
                try {
                    var empty = AgentScreenshot.Save(window, path, write: null, CancellationToken.None)
                        .GetAwaiter().GetResult();
                    var root = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
                    var dpi = VisualTreeHelper.GetDpi(window);
                    var decoder = new PngBitmapDecoder(
                        new Uri(path),
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);
                    var frame = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
                    var pixel = new byte[4];
                    frame.CopyPixels(new Int32Rect(frame.PixelWidth / 2, frame.PixelHeight / 2, 1, 1), pixel, 4, 0);
                    var width = (int)Math.Ceiling(root.ActualWidth * dpi.DpiScaleX);
                    var height = (int)Math.Ceiling(root.ActualHeight * dpi.DpiScaleY);
                    using (Assert.EnterMultipleScope()) {
                        Assert.That(empty, Is.False);
                        Assert.That(frame.PixelWidth, Is.EqualTo(width));
                        Assert.That(frame.PixelHeight, Is.EqualTo(height));
                        Assert.That(pixel, Is.EqualTo(new byte[] { 0x00, 0x00, 0xFF, 0xFF }));
                    }
                }
                finally {
                    window.Close();
                }
            });
        }
        finally {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public void Save_CanceledWhileWriting_PreservesDestinationAndDiscardsStagedFile() {
        var directory = Directory.CreateTempSubdirectory("BrowsAgentScreenshotTest-");
        try {
            var path = Path.Combine(directory.FullName, "window.png");
            File.WriteAllText(path, "previous contents");
            DispatcherTools.Run(dispatcher => {
                var window = ShowWindow(new Border { Background = Brushes.Red });
                using var cancellation = new CancellationTokenSource();
                var writing = new TaskCompletionSource<(string Path, bool OnDispatcher)>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var temporary = default(string);
                Task write(string file, byte[] png, CancellationToken token) {
                    File.WriteAllBytes(file, png);
                    writing.SetResult((file, dispatcher.CheckAccess()));
                    return release.Task;
                }
                try {
                    var saving = AgentScreenshot.Save(window, path, write, cancellation.Token);
                    var stage = writing.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                    temporary = stage.Path;
                    Assert.That(stage.OnDispatcher, Is.False);
                    cancellation.Cancel();
                    Assert.That(
                        () => saving.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(),
                        Throws.InstanceOf<OperationCanceledException>());
                    Assert.That(File.ReadAllText(path), Is.EqualTo("previous contents"));
                }
                finally {
                    cancellation.Cancel();
                    release.TrySetResult();
                    window.Close();
                }
                Assert.That(
                    SpinWait.SpinUntil(() => !File.Exists(temporary), TimeSpan.FromSeconds(5)),
                    Is.True);
                Assert.That(File.ReadAllText(path), Is.EqualTo("previous contents"));
            });
        }
        finally {
            directory.Delete(recursive: true);
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(@"relative\window.png")]
    [TestCase(@"C:\shots\window.jpg")]
    public void ValidPath_PathIsNotAnAbsolutePng_Throws(string path) {
        Assert.That(() => AgentScreenshot.ValidPath(path), Throws.ArgumentException);
    }

    [Test]
    public void ValidPath_AbsolutePng_ReturnsFullPath() {
        Assert.That(AgentScreenshot.ValidPath(@"C:\shots\..\window.PNG"), Is.EqualTo(@"C:\window.PNG"));
    }
}
