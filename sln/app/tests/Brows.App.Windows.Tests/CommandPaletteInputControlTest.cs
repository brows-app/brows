using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Brows;

[TestFixture]
internal sealed class CommandPaletteInputControlTest {
    private static ResourceDictionary Load(string assembly, string path) {
        return new ResourceDictionary {
            Source = new Uri($"pack://application:,,,/{assembly};component/{path}.xaml")
        };
    }

    [TestCase("Light")]
    [TestCase("Dark")]
    public void TextSuggestion_TracksInputHorizontalScrolling(string mode) {
        DispatcherTools.Run(dispatcher => {
            var window = new Window {
                Width = 280,
                Height = 180,
                Left = -10000,
                ShowActivated = false,
                ShowInTaskbar = false
            };
            try {
                window.Resources.MergedDictionaries.Add(
                    Load("Brows.Framework.Windows", $"Resources/Themes/{mode}.FileExplorer"));
                window.Resources.MergedDictionaries.Add(
                    Load("Brows.Commander.Windows", "Resources/Styles/Brows.Style.CommandPaletteInput"));
                var control = new CommandPaletteInputControl();
                window.Content = control;
                window.Show();
                window.UpdateLayout();
                var input = (TextBox)control.Template.FindName("PART_Text", control);
                var suggestion = (TextBox)control.Template.FindName("PART_TextSuggestion", control);
                var prefix = new string('W', 80);
                input.Text = "W";
                suggestion.Text = prefix;
                window.UpdateLayout();
                dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.That(suggestion.HorizontalOffset, Is.EqualTo(0));
                Assert.That(suggestion.GetRectFromCharacterIndex(0).X,
                    Is.EqualTo(input.GetRectFromCharacterIndex(0).X).Within(0.1));
                input.Text = prefix;
                suggestion.Text = prefix + " completion";
                input.CaretIndex = input.Text.Length;
                input.ScrollToHorizontalOffset(200);
                window.UpdateLayout();
                dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.That(input.HorizontalOffset, Is.GreaterThan(0));
                Assert.That(suggestion.HorizontalOffset, Is.EqualTo(input.HorizontalOffset).Within(0.1));

                suggestion.Text = prefix + new string('x', 100);
                window.UpdateLayout();
                dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.That(suggestion.HorizontalOffset, Is.EqualTo(input.HorizontalOffset).Within(0.1));

                input.CaretIndex = 0;
                input.ScrollToHorizontalOffset(0);
                window.UpdateLayout();
                dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.That(input.HorizontalOffset, Is.EqualTo(0));
                Assert.That(suggestion.HorizontalOffset, Is.EqualTo(0));
            }
            finally {
                window.Close();
            }
        });
    }
}
