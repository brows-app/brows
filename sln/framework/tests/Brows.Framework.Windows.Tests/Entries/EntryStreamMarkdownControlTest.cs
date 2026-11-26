using Brows.Windows.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryStreamMarkdownControlTest {
    [TestCase(0, 14, "Markdown", TestName = "Preview_UnknownSizeBelowByteLimit_DisplaysFullMarkdown")]
    [TestCase(0, 15, "Markdown", TestName = "Preview_UnknownSizeAtByteLimit_DisplaysFullMarkdown")]
    [TestCase(0, 16, "Unavailable", TestName = "Preview_UnknownSizeOverByteLimit_RejectsPartialMarkdown")]
    [TestCase(14, 16, "Unavailable", TestName = "Preview_StaleSizeOverByteLimit_RejectsPartialMarkdown")]
    public void Preview_AdvertisedSize_ChecksActualByteLength(long advertisedSize, int byteLength, string expectedView) {
        var markdown = "# " + new string('x', byteLength - 2);
        var source = new Mock<IEntryStreamSource>();
        source.SetupGet(item => item.StreamLength).Returns(advertisedSize);
        source.SetupGet(item => item.Stream).Returns(() => new MemoryStream(Encoding.UTF8.GetBytes(markdown)));
        source.Setup(item => item.StreamReady(It.IsAny<CancellationToken>())).ReturnsAsync(new EntryStreamReady());

        DispatcherTools.Run(dispatcher => {
            var gui = new MarkdownGui(source.Object);
            var control = new EntryStreamMarkdownControl { ViewModel = gui };
            var window = new Window {
                Content = control,
                ShowInTaskbar = false,
                ShowActivated = false,
                Left = -10000
            };
            window.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri("/Brows.Framework.Windows;component/Resources/Resources.xaml", UriKind.Relative)
            });
            try {
                window.Show();
                window.UpdateLayout();
                var elapsed = Stopwatch.StartNew();
                while (gui.View is null or "Loading" && elapsed.Elapsed < TimeSpan.FromSeconds(5)) {
                    DispatcherTools.Pump(dispatcher);
                }

                var viewer = (MarkdownControl)control.Template.FindName("PART_MarkdownControl", control);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(gui.View, Is.EqualTo(expectedView));
                    Assert.That(viewer.Markdown, Is.EqualTo(expectedView == "Markdown" ? markdown : null));
                }
            }
            finally {
                window.Close();
            }
        });
    }

    private sealed class MarkdownGui : EntryStreamGui {
        public sealed override bool ForceMarkdown => true;

        public sealed override IEntryStreamSource Source { get; }

        public sealed override IEntryStreamGuiOptions Options { get; } = new EntryStreamGuiOptions {
            Markdown = new EntryStreamGuiOptions.MarkdownOptions { SourceLengthMax = 15 }
        };

        public MarkdownGui(IEntryStreamSource source) {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }
    }
}
