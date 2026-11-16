using Brows.Windows.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Brows.Windows;

[TestFixture]
[NonParallelizable]
internal sealed class AppThemeTest {
    private static ResourceDictionary Palette(ResourceDictionary resources) {
        return resources.MergedDictionaries[0].MergedDictionaries[0];
    }

    private static string Source(ResourceDictionary dictionary) {
        return dictionary.Source?.OriginalString ?? string.Empty;
    }

    [Test]
    public void LoadExportResources_MergesFrameworkCultureWithoutCompatibilityBridge() {
        DispatcherTools.Run(_ => {
            var window = new Window();
            try {
                /*
                 * WPF initializes pack URI parsing when its first window is created.
                 * The test host does not run the application's startup path.
                 */
                var component = new AppComponent(typeof(ValueConverter).Assembly);
                var compatibilityDictionaries = component.ResourceDictionaries.ToArray();
                var appDictionaries = component.ApplicationResourceDictionaries;
                var appResources = new ResourceDictionary();
                var theme = new AppTheme(appResources);
                var followingDictionary = new ResourceDictionary();
                followingDictionary["Review.ExportResource"] = "Available";

                IEnumerable<ResourceDictionary> OrderedDictionaries() {
                    foreach (var dictionary in appDictionaries) {
                        yield return dictionary;
                        Assert.That(appResources.MergedDictionaries.Any(existing =>
                            ReferenceEquals(existing, dictionary)), Is.True);
                    }
                    yield return followingDictionary;
                }

                theme.LoadExportResources(OrderedDictionaries());

                using (Assert.EnterMultipleScope()) {
                    Assert.That(compatibilityDictionaries.Any(dictionary =>
                        Source(dictionary).EndsWith("/Resource/Resource.xaml", StringComparison.Ordinal)), Is.True);
                    Assert.That(compatibilityDictionaries.Any(dictionary =>
                        Source(dictionary).EndsWith("/Resource/Culture/_.xaml", StringComparison.Ordinal)), Is.True);
                    Assert.That(appResources.MergedDictionaries.Any(dictionary =>
                        Source(dictionary).EndsWith("/Resource/Culture/_.xaml", StringComparison.Ordinal)), Is.True);
                    Assert.That(appResources.MergedDictionaries.Any(dictionary =>
                        Source(dictionary).EndsWith("/Resource/Resource.xaml", StringComparison.Ordinal)), Is.False);
                    Assert.That(appResources.MergedDictionaries.Any(dictionary =>
                        ReferenceEquals(dictionary, followingDictionary)), Is.True);
                    Assert.That(appResources["Review.ExportResource"], Is.EqualTo("Available"));
                    Assert.That(appResources.MergedDictionaries
                        .SelectMany(dictionary => dictionary.MergedDictionaries)
                        .Count(dictionary => Source(dictionary).EndsWith(
                            "/Resources/Styles.xaml", StringComparison.Ordinal)), Is.EqualTo(1));
                }

                var ignoredDictionary = new ResourceDictionary();
                theme.LoadExportResources([ignoredDictionary]);
                Assert.That(appResources.MergedDictionaries.Any(dictionary =>
                    ReferenceEquals(dictionary, ignoredDictionary)), Is.False);
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void Apply_InvalidThemePreservesCurrentPaletteAndLaterValidThemeApplies() {
        DispatcherTools.Run(_ => {
            var window = new Window();
            try {
                /*
                 * WPF initializes pack URI parsing when its first window is created.
                 * The test host does not run the application's startup path.
                 */
                var appResources = new ResourceDictionary();
                var theme = new AppTheme(appResources);
                theme.Apply(new CommanderTheme {
                    Name = "FileExplorer",
                    Mode = CommanderThemeMode.Light
                });
                var lightPalette = Palette(appResources);

                Assert.Throws<ArgumentException>(() => theme.Apply(new CommanderTheme {
                    Name = "DarkGreen",
                    Mode = CommanderThemeMode.Dark
                }));
                Assert.That(Palette(appResources), Is.SameAs(lightPalette));

                Assert.Throws<ArgumentOutOfRangeException>(() => theme.Apply(new CommanderTheme {
                    Name = "FileExplorer",
                    Mode = (CommanderThemeMode)5
                }));
                Assert.That(Palette(appResources), Is.SameAs(lightPalette));

                theme.Apply(new CommanderTheme {
                    Name = "FileExplorer",
                    Mode = CommanderThemeMode.Dark
                });

                using (Assert.EnterMultipleScope()) {
                    Assert.That(Palette(appResources), Is.Not.SameAs(lightPalette));
                    Assert.That(Source(Palette(appResources)).EndsWith(
                        "/Themes/Dark.FileExplorer.xaml", StringComparison.Ordinal), Is.True);
                }
            }
            finally {
                window.Close();
            }
        });
    }
}
