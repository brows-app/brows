using Brows.Entries;
using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Brows.Resource;

[TestFixture]
internal sealed class ResourceTest {
    private static ResourceDictionary Load(string name) {
        return new ResourceDictionary {
            Source = new Uri($"pack://application:,,,/Brows.Framework.Windows;component/Resources/{name}.xaml")
        };
    }

    private static void Materialize(ResourceDictionary resources) {
        foreach (var merged in resources.MergedDictionaries) {
            Materialize(merged);
        }
        foreach (DictionaryEntry entry in resources) {
            if (entry.Value is Style style) {
                Materialize(style.Resources);
            }
        }
    }

    [TestCase("Light")]
    [TestCase("Dark")]
    public void Resources_ThemeBrushesAreFrozen(string mode) {
        DispatcherTools.Run(dispatcher => {
            var theme = Load($"Themes/{mode}.FileExplorer");
            var brushCount = 0;
            foreach (DictionaryEntry entry in theme) {
                if (entry.Value is Brush brush) {
                    Assert.That(brush.IsFrozen, Is.True, $"{mode}: {entry.Key}");
                    brushCount++;
                }
            }
            Assert.That(brushCount, Is.GreaterThan(100));
            var thumb = (SolidColorBrush)theme["Brows.Theme.ScrollBar.Static.Thumb"];
            Assert.That(thumb.Color, Is.EqualTo(theme["Brows.Theme.Color.Foreground"]));
            Assert.That(thumb.Opacity, Is.EqualTo(0.4));
            var style = (Style)Load("Styles/Brows.Style.ProviderTreeViewItem")["Brows.Style.ProviderTreeViewItem"];
            Assert.That(((Geometry)style.Resources["TreeArrow"]).IsFrozen, Is.True);
        });
    }

    [TestCase("Light", "#FF1A1A1A")]
    [TestCase("Dark", "#FFF3F3F3")]
    public void Resources_DefaultControlsUseTheme(string mode, string foreground) {
        DispatcherTools.Run(dispatcher => {
            var window = new Window { ShowInTaskbar = false, ShowActivated = false, Left = -10000 };
            try {
                window.Resources.MergedDictionaries.Add(Load($"Themes/{mode}.FileExplorer"));
                window.Resources.MergedDictionaries.Add(Load("Resources"));
                Materialize(window.Resources);
                var text = new TextBlock { Text = "Entry" };
                var button = new Button { Content = "Apply" };
                var combo = new ComboBox { IsEditable = true, ItemsSource = new[] { "First", "Second" } };
                var progress = new ProgressBar { IsIndeterminate = true, Width = 200 };
                var entryIcon = new EntryIconImage();
                var panel = new StackPanel();
                panel.Children.Add(text);
                panel.Children.Add(button);
                panel.Children.Add(combo);
                panel.Children.Add(progress);
                panel.Children.Add(entryIcon);
                window.Content = panel;
                window.Show();
                window.UpdateLayout();

                Assert.That(text.Foreground.ToString(), Is.EqualTo(foreground));
                Assert.That(button.FontFamily.Source, Is.EqualTo("Segoe UI"));
                Assert.That(button.FontSize, Is.EqualTo(12));
                Assert.That(entryIcon.Width, Is.EqualTo(16));
                Assert.That(entryIcon.Height, Is.EqualTo(16));
                Assert.That(combo.Template.FindName("PART_EditableTextBox", combo), Is.TypeOf<TextBox>());
                Assert.That(combo.Template.FindName("PART_Popup", combo), Is.TypeOf<Popup>());
                var indicator = (Border)progress.Template.FindName("PART_Indicator", progress);
                Assert.That(indicator.HasAnimatedProperties, Is.True);
                var trigger = (Trigger)progress.Template.Triggers[0];
                var animation = ((BeginStoryboard)trigger.EnterActions[0]).Storyboard;
                Assert.That(animation.IsFrozen, Is.True);
                Assert.That(button.IsTabStop, Is.True);
                var editableText = (TextBox)combo.Template.FindName("PART_EditableTextBox", combo);
                Assert.That(editableText.IsTabStop, Is.True);
                Assert.That(editableText.Visibility, Is.EqualTo(Visibility.Visible));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Resources_ThemeSwitchUpdatesExistingControls() {
        DispatcherTools.Run(dispatcher => {
            var window = new Window { ShowInTaskbar = false, ShowActivated = false, Left = -10000 };
            try {
                window.Resources.MergedDictionaries.Add(Load("Themes/Dark.FileExplorer"));
                var resources = Load("Resources");
                window.Resources.MergedDictionaries.Add(resources);
                window.Resources.MergedDictionaries.Add(new ResourceDictionary {
                    Source = new Uri("pack://application:,,,/Brows.Framework.Windows;component/Resource/Resource.xaml")
                });
                var text = new TextBlock { Text = "Entry" };
                var panel = new StackPanel();
                panel.Children.Add(text);
                var scroll = new ScrollBar { Maximum = 100, ViewportSize = 10, Height = 200 };
                panel.Children.Add(scroll);
                window.Content = panel;
                window.Show();
                window.UpdateLayout();
                Assert.That(text.Foreground.ToString(), Is.EqualTo("#FFF3F3F3"));
                var track = (Track)scroll.Template.FindName("PART_Track", scroll);
                track.Thumb.ApplyTemplate();
                var thumb = (Rectangle)track.Thumb.Template.FindName("rectangle", track.Thumb);
                Assert.That(thumb.Fill.ToString(), Is.EqualTo("#FFF3F3F3"));
                Assert.That(thumb.Fill.Opacity, Is.EqualTo(0.4));

                window.Resources.MergedDictionaries[0] = Load("Themes/Light.FileExplorer");
                window.UpdateLayout();

                Assert.That(text.Foreground.ToString(), Is.EqualTo("#FF1A1A1A"));
                Assert.That(thumb.Fill.ToString(), Is.EqualTo("#FF1A1A1A"));
                Assert.That(thumb.Fill.IsFrozen, Is.True);
                Assert.That(window.Resources.MergedDictionaries[1], Is.SameAs(resources));
            }
            finally {
                window.Close();
            }
        });
    }
}
