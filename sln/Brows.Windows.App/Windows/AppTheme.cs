using Brows.Gui;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Brows.Windows {
    internal sealed class AppTheme {
        private static readonly ILog Log = Logging.For(typeof(AppTheme));

        private static readonly IReadOnlyDictionary<string, Uri> BaseUri =
            new[] { "Dark", "Light" }.ToDictionary(
                key => key,
                key => LocalUri($"Base/{key}"),
                StringComparer.OrdinalIgnoreCase);

        private AppTheme(WindowsApplication app, CommanderTheme theme = null) {
            App = app ?? throw new ArgumentNullException(nameof(app));
            theme = theme ?? new CommanderTheme { Base = BaseUri.Keys.First() };
            var key = theme.Base?.Trim() ?? "";
            var background = theme.Background?.Trim() ?? "";
            var foreground = theme.Foreground?.Trim() ?? "";
            var uriKey = BaseUri.ContainsKey(key) ? key : BaseUri.Keys.First();
            var uri = BaseUri[uriKey];
            var rd = new ResourceDictionary { Source = uri };
            if (foreground != "") {
                try {
                    rd["Global_Default_Foreground"] = (Color)ColorConverter.ConvertFromString(foreground);
                }
                catch (Exception ex) {
                    if (Log.Warn()) {
                        Log.Warn(ex);
                    }
                }
            }
            if (background != "") {
                try {
                    rd["Global_Default_Background"] = (Color)ColorConverter.ConvertFromString(background);
                }
                catch (Exception ex) {
                    if (Log.Warn()) {
                        Log.Warn(ex);
                    }
                }
            }
            App.Resources.Clear();
            App.Resources.MergedDictionaries.Clear();
            App.Resources.MergedDictionaries.Add(rd);
            foreach (var dictionary in LocalResourceDictionaries()) {
                App.Resources.MergedDictionaries.Add(dictionary);
            }
            foreach (var dictionary in App.Exports.Components.ResourceDictionaries) {
                App.Resources.MergedDictionaries.Add(dictionary);
            }
        }

        private static Uri LocalUri(string name) {
            return new Uri($"pack://application:,,,/Brows.Windows.App;component/Theme/{name}.xaml");
        }

        private static ResourceDictionary LocalResource(string name) {
            return new ResourceDictionary {
                Source = LocalUri(name)
            };
        }

        private static IEnumerable<ResourceDictionary> LocalResourceDictionaries() {
            yield return LocalResource("Extend");
        }

        public WindowsApplication App { get; }

        public sealed class Service : ICommanderTheme {
            public WindowsApplication App { get; }

            public Service(WindowsApplication app) {
                App = app;
            }

            public void Apply(CommanderTheme theme) {
                new AppTheme(App, theme);
            }
        }
    }
}
