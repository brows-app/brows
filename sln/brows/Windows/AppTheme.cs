using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Brows.Windows {
    using Logger;

    internal class AppTheme {
        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(AppTheme)));
        private ILog _Log;

        private static readonly IReadOnlyDictionary<string, Uri> BaseUri =
            new[] { "Dark", "Light" }.ToDictionary(
                key => key,
                key => LocalUri($"Base/{key}"),
                StringComparer.OrdinalIgnoreCase);

        private static Uri LocalUri(string name) {
            return new Uri($"pack://application:,,,/Theme/{name}.xaml");
        }

        private static ResourceDictionary LocalResource(string name) {
            return new ResourceDictionary {
                Source = LocalUri(name)
            };
        }

        private static IEnumerable<ResourceDictionary> LocalResourceDictionaries() {
            yield return LocalResource("Extend");
        }

        public Application Application { get; }
        public AppComponentCollection Components { get; }

        public AppTheme(Application application, AppComponentCollection components, CommanderTheme theme = null) {
            Application = application ?? throw new ArgumentNullException(nameof(application));
            Components = components ?? throw new ArgumentNullException(nameof(components));

            theme = theme ?? new CommanderTheme("dark", null, null);
            var key = theme.Base?.Trim() ?? "";
            var background = theme.Background?.Trim() ?? "";
            var foreground = theme.Foreground?.Trim() ?? "";

            if (BaseUri.TryGetValue(key, out var colorUri)) {
                var dict = new ResourceDictionary { Source = colorUri };
                if (foreground != "") {
                    try {
                        dict["Global_Default_Foreground"] = (Color)ColorConverter.ConvertFromString(foreground);
                    }
                    catch (Exception ex) {
                        if (Log.Warn()) {
                            Log.Warn(ex);
                        }
                    }
                }
                if (background != "") {
                    try {
                        dict["Global_Default_Background"] = (Color)ColorConverter.ConvertFromString(background);
                    }
                    catch (Exception ex) {
                        if (Log.Warn()) {
                            Log.Warn(ex);
                        }
                    }
                }
                Application.Resources.Clear();
                Application.Resources.MergedDictionaries.Clear();
                Application.Resources.MergedDictionaries.Add(dict);
                foreach (var dictionary in LocalResourceDictionaries()) {
                    Application.Resources.MergedDictionaries.Add(dictionary);
                }
                foreach (var dictionary in Components.ResourceDictionaries) {
                    Application.Resources.MergedDictionaries.Add(dictionary);
                }
            }
        }
    }
}
