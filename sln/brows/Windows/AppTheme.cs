using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        private static ResourceDictionary SourceResource(string source) {
            try {
                return new ResourceDictionary { Source = new Uri(source) };
            }
            catch {
                return null;
            }
        }

        private static IEnumerable<string> CultureChecks() {
            var culture = Thread.CurrentThread.CurrentUICulture;
            var cultureName = culture.Name;
            yield return cultureName;

            var parentName = culture.Parent?.Name;
            if (parentName != null && parentName != cultureName) {
                yield return parentName;
            }

            var defaultName = "_";
            yield return defaultName;
        }

        private IEnumerable<ResourceDictionary> ComponentResourceDictionaries() {
            foreach (var componentResource in ComponentResources) {
                var type = componentResource.GetType();
                var assembly = type.Assembly;
                var assemblyName = assembly.GetName();
                var resource = SourceResource($"pack://application:,,,/{assemblyName.Name};component/Resource/Resource.xaml");
                if (resource != null) {
                    yield return resource;
                }
                var cultureChecks = CultureChecks();
                foreach (var culture in cultureChecks) {
                    var cultureResource = SourceResource($"pack://application:,,,/{assemblyName.Name};component/Resource/Culture/{culture}.xaml");
                    if (cultureResource != null) {
                        yield return cultureResource;
                        break;
                    }
                }
            }
        }

        public Application Application { get; }
        public IEnumerable<IComponentResource> ComponentResources { get; }

        public AppTheme(Application application, IEnumerable<IComponentResource> componentResources, CommanderTheme theme = null) {
            Application = application ?? throw new ArgumentNullException(nameof(application));
            ComponentResources = componentResources ?? throw new ArgumentNullException(nameof(componentResources));

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
                foreach (var dictionary in ComponentResourceDictionaries()) {
                    Application.Resources.MergedDictionaries.Add(dictionary);
                }
            }
        }
    }
}
