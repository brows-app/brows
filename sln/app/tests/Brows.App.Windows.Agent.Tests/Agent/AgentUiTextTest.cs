using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;

namespace Brows.Agent;

[TestFixture]
[NonParallelizable]
internal sealed class AgentUiTextTest {
    private sealed record Row(string Name, string Size);

    private static Window ShowWindow(UIElement content) {
        var window = new Window {
            Content = content,
            Height = 300,
            Left = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
            Title = "Test",
            Top = 0,
            Width = 320
        };
        window.Show();
        window.UpdateLayout();
        DispatcherTools.Pump(window.Dispatcher);
        return window;
    }

    private static StackPanel Panel(params UIElement[] children) {
        var panel = new StackPanel();
        foreach (var child in children) {
            panel.Children.Add(child);
        }
        return panel;
    }

    private static IReadOnlyList<string> Body(string text) {
        return text.Split('\n').Skip(2).ToList();
    }

    private static UIElement Content(out TextBox input) {
        var scrolled = new ScrollViewer {
            Content = Panel(
                new TextBlock { Text = "top" },
                new Border { Height = 200 },
                new TextBlock { Text = "below" }),
            Height = 20,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden
        };
        var list = new ListBox { ItemsSource = new[] { "one", "two" }, SelectedIndex = 0 };
        var inlines = new TextBlock();
        inlines.Inlines.Add(new Run("a"));
        inlines.Inlines.Add(new Bold(new Run("b")));
        input = new TextBox { Name = "input", Text = "abc" };
        return new AgentTestView(Panel(
            new TextBlock { Text = "hello" },
            new TextBlock { Text = "hidden", Visibility = Visibility.Collapsed },
            new TextBlock { Text = "transparent", Opacity = 0 },
            list,
            input,
            scrolled,
            inlines));
    }

    [Test]
    public void Write_WritesTheVisibleTextOfTheWindow() {
        DispatcherTools.Run(dispatcher => {
            var window = ShowWindow(Content(out var input));
            try {
                input.Focus();
                input.CaretIndex = 3;
                DispatcherTools.Pump(dispatcher);
                var text = AgentUiText.Write(window);
                Assert.That(Body(text), Is.EqualTo(new[] {
                    "- AgentTestView",
                    "  \"hello\"",
                    "  > [selected] one",
                    "  > two",
                    "  textbox input [focus] \"abc\" caret 3 selection 3+0",
                    "  \"top\"",
                    "  \"ab\""
                }));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Write_StartsWithTheWindowAndFocus() {
        DispatcherTools.Run(dispatcher => {
            var window = ShowWindow(Content(out var input));
            try {
                input.Focus();
                DispatcherTools.Pump(dispatcher);
                var lines = AgentUiText.Write(window).Split('\n');
                using (Assert.EnterMultipleScope()) {
                    Assert.That(lines[0], Does.StartWith($"window {AgentWindow.Id(window)} \"Test\""));
                    Assert.That(lines[0], Does.EndWith(" 320x300"));
                    Assert.That(lines[1], Is.EqualTo("focus TextBox input in AgentTestView"));
                }
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Write_WithoutFocus_WritesFocusNone() {
        DispatcherTools.Run(dispatcher => {
            var window = ShowWindow(new AgentTestView(new TextBlock { Text = "hello" }));
            try {
                Keyboard.ClearFocus();
                DispatcherTools.Pump(dispatcher);
                var lines = AgentUiText.Write(window).Split('\n');
                Assert.That(lines[1], Is.EqualTo("focus none"));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Write_TruncatesAfterMaxLines() {
        DispatcherTools.Run(dispatcher => {
            var window = ShowWindow(Content(out _));
            try {
                var lines = AgentUiText.Write(window, maxLines: 4).Split('\n');
                Assert.That(lines, Has.Length.EqualTo(5));
                Assert.That(lines[4], Is.EqualTo("… 5 more lines"));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Write_KeepsPathsAndQuotesAndMarksLineBreaks() {
        DispatcherTools.Run(dispatcher => {
            var window = ShowWindow(new TextBlock { Text = "say \"hi\"\r\nC:\\new\tdir" });
            try {
                var body = Body(AgentUiText.Write(window));
                Assert.That(body, Is.EqualTo(new[] { "\"say \"hi\"⏎C:\\new dir\"" }));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Write_WritesGridViewHeadersAndRows() {
        DispatcherTools.Run(dispatcher => {
            var view = new GridView();
            view.Columns.Add(new GridViewColumn {
                DisplayMemberBinding = new Binding(nameof(Row.Name)),
                Header = "Name"
            });
            view.Columns.Add(new GridViewColumn {
                DisplayMemberBinding = new Binding(nameof(Row.Size)),
                Header = "Size"
            });
            var list = new ListView { ItemsSource = new[] { new Row("a.txt", "1 KB") }, View = view };
            var window = ShowWindow(list);
            try {
                var body = Body(AgentUiText.Write(window));
                Assert.That(body, Is.EqualTo(new[] { "| Name | Size", "> a.txt | 1 KB" }));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Write_WritesReorderedGridColumnsInDisplayOrder() {
        DispatcherTools.Run(dispatcher => {
            var view = new GridView();
            view.Columns.Add(new GridViewColumn {
                DisplayMemberBinding = new Binding(nameof(Row.Name)),
                Header = "Name"
            });
            view.Columns.Add(new GridViewColumn {
                DisplayMemberBinding = new Binding(nameof(Row.Size)),
                Header = "Size"
            });
            var list = new ListView { ItemsSource = new[] { new Row("a.txt", "1 KB") }, View = view };
            var window = ShowWindow(list);
            try {
                view.Columns.Move(1, 0);
                window.UpdateLayout();
                DispatcherTools.Pump(dispatcher);
                var body = Body(AgentUiText.Write(window));
                Assert.That(body, Is.EqualTo(new[] { "| Size | Name", "> 1 KB | a.txt" }));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Write_PreservesEmptyGridCellsAfterColumnsAreReordered() {
        DispatcherTools.Run(dispatcher => {
            var view = new GridView();
            view.Columns.Add(new GridViewColumn {
                Header = "Name",
                DisplayMemberBinding = new Binding(nameof(Row.Name)),
                Width = 90
            });
            view.Columns.Add(new GridViewColumn {
                Header = "Size",
                DisplayMemberBinding = new Binding(nameof(Row.Size)),
                Width = 90
            });
            var list = new ListView { ItemsSource = new[] { new Row("Folder", null) }, View = view };
            var window = ShowWindow(list);
            try {
                view.Columns.Move(1, 0);
                window.UpdateLayout();
                DispatcherTools.Pump(dispatcher);
                var body = Body(AgentUiText.Write(window));
                Assert.That(body, Is.EqualTo(new[] { "| Size | Name", ">  | Folder" }));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Write_MaxLinesIsNotPositive_Throws() {
        DispatcherTools.Run(dispatcher => {
            var window = ShowWindow(new TextBlock());
            try {
                Assert.That(
                    () => AgentUiText.Write(window, maxLines: 0),
                    Throws.InstanceOf<ArgumentOutOfRangeException>());
            }
            finally {
                window.Close();
            }
        });
    }
}
