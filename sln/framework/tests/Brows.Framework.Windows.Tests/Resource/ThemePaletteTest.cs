using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Brows.Resource;

[TestFixture]
internal sealed class ThemePaletteTest {
    private static ResourceDictionary Load(string mode) {
        return new ResourceDictionary {
            Source = new Uri(
                $"pack://application:,,,/Brows.Framework.Windows;component/Resources/Themes/{mode}.FileExplorer.xaml")
        };
    }

    private static void CollectKeys(ResourceDictionary dictionary, HashSet<object> keys) {
        foreach (var merged in dictionary.MergedDictionaries) {
            CollectKeys(merged, keys);
        }
        foreach (var key in dictionary.Keys) {
            keys.Add(key);
        }
    }

    private static void AssertNoFonts(ResourceDictionary dictionary) {
        foreach (var merged in dictionary.MergedDictionaries) {
            AssertNoFonts(merged);
        }
        foreach (DictionaryEntry entry in dictionary) {
            var key = entry.Key.ToString();
            Assert.That(key, Does.Not.Contain(".Font"), key);
            Assert.That(entry.Value, Is.Not.InstanceOf<FontFamily>(), key);
            Assert.That(entry.Value, Is.Not.TypeOf<FontWeight>(), key);
            Assert.That(entry.Value, Is.Not.TypeOf<FontStyle>(), key);
            Assert.That(entry.Value, Is.Not.TypeOf<FontStretch>(), key);
        }
    }

    [Test]
    public void Themes_LightAndDarkExposeTheSameKeys() {
        DispatcherTools.Run(dispatcher => {
            /*
             * A window initializes WPF's application pack-URI handling in the test host.
             */
            var window = new Window();
            try {
                var light = new HashSet<object>();
                var dark = new HashSet<object>();
                CollectKeys(Load("Light"), light);
                CollectKeys(Load("Dark"), dark);

                Assert.That(light, Does.Contain("Brows.Theme.TextSelection"));
                Assert.That(light, Does.Contain("Brows.Theme.Color.RowFocus"));
                Assert.That(dark, Is.EquivalentTo(light));
            }
            finally {
                window.Close();
            }
        });
    }

    [TestCase("Light")]
    [TestCase("Dark")]
    public void Themes_DoNotDefineFontResources(string mode) {
        DispatcherTools.Run(dispatcher => {
            var window = new Window();
            try {
                AssertNoFonts(Load(mode));
            }
            finally {
                window.Close();
            }
        });
    }
}
