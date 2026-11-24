using System;
using System.Windows;
using System.Windows.Controls;

namespace Brows.Agent;

[TestFixture]
[NonParallelizable]
internal sealed class AgentWindowTest {
    private static ResourceDictionary Load(string assembly, string path) {
        return new ResourceDictionary {
            Source = new Uri($"pack://application:,,,/{assembly};component/{path}.xaml")
        };
    }

    private static Window ShowWindow(UIElement content) {
        var window = new Window {
            Content = content,
            Height = 180,
            Left = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
            Title = "Palette",
            Top = 0,
            Width = 280
        };
        window.Resources.MergedDictionaries.Add(
            Load("Brows.Framework.Windows", "Resources/Themes/Light.FileExplorer"));
        window.Resources.MergedDictionaries.Add(
            Load("Brows.Commander.Windows", "Resources/Styles/Brows.Style.CommandPaletteInput"));
        window.Show();
        window.UpdateLayout();
        DispatcherTools.Pump(window.Dispatcher);
        return window;
    }

    [Test]
    public void Palette_WithoutPalette_IsClosed() {
        DispatcherTools.Run(_ => {
            var window = ShowWindow(new TextBox());
            try {
                Assert.That(AgentWindow.Palette(window), Is.EqualTo(new AgentPalette(false, false, null)));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Palette_WithFocusedInput_ReportsItsText() {
        DispatcherTools.Run(dispatcher => {
            var control = new CommandPaletteInputControl();
            var window = ShowWindow(control);
            try {
                var input = (TextBox)control.Template.FindName("PART_Text", control);
                input.Text = "panel-add";
                input.Focus();
                DispatcherTools.Pump(dispatcher);
                Assert.That(AgentWindow.Palette(window), Is.EqualTo(new AgentPalette(true, true, "panel-add")));
                Assert.That(AgentWindow.Focus(window), Is.EqualTo(
                    new AgentFocus("TextBox", "PART_Text", nameof(CommandPaletteInputControl))));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Palette_WithInputNotFocused_IsOpenButNotFocused() {
        DispatcherTools.Run(dispatcher => {
            var other = new TextBox();
            var control = new CommandPaletteInputControl();
            var panel = new StackPanel();
            panel.Children.Add(control);
            panel.Children.Add(other);
            var window = ShowWindow(panel);
            try {
                other.Focus();
                DispatcherTools.Pump(dispatcher);
                var palette = AgentWindow.Palette(window);
                Assert.That(palette.Open, Is.True);
                Assert.That(palette.Focused, Is.False);
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Info_DescribesTheWindow() {
        DispatcherTools.Run(_ => {
            var window = ShowWindow(new TextBox());
            try {
                var info = AgentWindow.Info(window, 2);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(info.Id, Is.Not.Zero);
                    Assert.That(info.Id, Is.EqualTo(AgentWindow.Id(window)));
                    Assert.That(info.Index, Is.EqualTo(2));
                    Assert.That(info.Title, Is.EqualTo("Palette"));
                    Assert.That(info.Width, Is.EqualTo(280));
                    Assert.That(info.Height, Is.EqualTo(180));
                }
            }
            finally {
                window.Close();
            }
        });
    }
}
