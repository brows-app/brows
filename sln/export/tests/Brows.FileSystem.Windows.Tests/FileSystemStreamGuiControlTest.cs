using Brows.Conf;
using Brows.Entries;
using Brows.Windows.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using TEXTREADER = Domore.Windows.Controls.TextReader;

namespace Brows;

[TestFixture]
internal sealed class FileSystemStreamGuiControlTest {
    private static async Task WithPreview(string extension, string source,
                                          Action<FileSystemStreamGuiControl, Action<Func<bool>>> inspect) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await new ConfigurationFile().File<FileSystemConfig>().Load(timeout.Token);

        var directory = Directory.CreateTempSubdirectory("BrowsMarkdownPreview");
        var path = Path.Combine(directory.FullName, $"preview{extension}");
        try {
            await File.WriteAllTextAsync(path, source, timeout.Token);
            var provider = new FileSystemProvider(new FileSystemProviderFactory(), directory,
                caseSensitive: false, initialCapacity: 0);
            var entry = new FileSystemEntry(provider, new FileInfo(path));

            DispatcherTools.Run(dispatcher => {
                var preview = new FileSystemStreamGuiControl { ViewModel = (FileSystemStreamGui)entry.Stream };
                preview.Resources.MergedDictionaries.Add(new ResourceDictionary {
                    Source = new Uri("/Brows.Framework.Windows;component/Resources/Resources.xaml", UriKind.Relative)
                });
                preview.Resources.MergedDictionaries.Add(new ResourceDictionary {
                    Source = new Uri("/Brows.FileSystem.Windows;component/Resources/Styles/" +
                        "Brows.Style.FileSystemStreamGuiControl.xaml", UriKind.Relative)
                });
                var window = new Window {
                    Content = preview,
                    Left = -10000,
                    ShowActivated = false,
                    ShowInTaskbar = false
                };
                try {
                    window.Show();
                    window.UpdateLayout();

                    void WaitFor(Func<bool> condition) {
                        var elapsed = Stopwatch.StartNew();
                        while (!condition() && elapsed.Elapsed < TimeSpan.FromSeconds(5)) {
                            DispatcherTools.Pump(dispatcher);
                        }
                    }

                    inspect(preview, WaitFor);
                }
                finally {
                    window.Close();
                }
            });
        }
        finally {
            File.Delete(path);
            directory.Delete();
        }
    }

    private static T FindVisualChild<T>(DependencyObject element) where T : DependencyObject {
        if (element is T match) {
            return match;
        }
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++) {
            var child = FindVisualChild<T>(VisualTreeHelper.GetChild(element, index));
            if (child is not null) {
                return child;
            }
        }
        return null;
    }

    [TestCase(".md", TestName = "Preview_LowercaseMarkdownExtension_RendersHeading")]
    [TestCase(".MD", TestName = "Preview_UppercaseMarkdownExtension_RendersHeading")]
    public async Task Preview_MarkdownExtension_RendersHeading(string extension) {
        await WithPreview(extension, "# Rendered heading", (preview, waitFor) => {
            var markdown = FindVisualChild<EntryStreamMarkdownControl>(preview);
            Assert.That(markdown, Is.Not.Null);
            var renderer = FindVisualChild<MarkdownControl>(markdown);
            Assert.That(renderer, Is.Not.Null);
            var viewer = FindVisualChild<FlowDocumentScrollViewer>(renderer);
            Assert.That(viewer, Is.Not.Null);
            waitFor(() => viewer.Document?.Blocks.FirstBlock is not null);

            Assert.That(viewer.Document?.Blocks.FirstBlock, Is.TypeOf<Paragraph>());
            var heading = (Paragraph)viewer.Document.Blocks.FirstBlock;
            using (Assert.EnterMultipleScope()) {
                Assert.That(markdown.IsVisible, Is.True);
                Assert.That(heading.Style, Is.SameAs(renderer.MarkdownHeading1Style));
                Assert.That(new TextRange(heading.ContentStart, heading.ContentEnd).Text,
                    Is.EqualTo("Rendered heading"));
            }
        });
    }

    [TestCase(250_001, "a", TestName = "Preview_MarkdownExceedsCharacterLimit_ShowsPlainText")]
    [TestCase(2_001, "# Heading\n\n", TestName = "Preview_MarkdownExceedsBlockLimit_ShowsPlainText")]
    [TestCase(4_001, "*word* ", TestName = "Preview_MarkdownExceedsSyntaxNodeLimit_ShowsPlainText")]
    public async Task Preview_MarkdownExceedsRendererLimit_ShowsPlainText(int count, string fragment) {
        var source = new StringBuilder().Insert(0, fragment, count).ToString();
        await WithPreview(".md", source, (preview, waitFor) => {
            waitFor(() => preview.ViewModel.View == "Text");

            var reader = FindVisualChild<TEXTREADER>(preview);
            using (Assert.EnterMultipleScope()) {
                Assert.That(preview.ViewModel.View, Is.EqualTo("Text"));
                Assert.That(reader?.TextReaderSuccess, Is.True);
                Assert.That(reader?.IsVisible, Is.True);
            }
        });
    }
}
