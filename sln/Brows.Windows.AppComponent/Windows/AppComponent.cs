using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Brows.Windows {
    public sealed class AppComponent {
        private Type ComponentResourceType =>
            _ComponentResourceType ?? (
            _ComponentResourceType = ComponentResource.GetType());
        private Type _ComponentResourceType;

        private Assembly ComponentResourceAssembly =>
            _ComponentResourceAssembly ?? (
            _ComponentResourceAssembly = ComponentResourceType.Assembly);
        private Assembly _ComponentResourceAssembly;

        private AssemblyName ComponentResourceAssemblyName =>
            _ComponentResourceAssemblyName ?? (
            _ComponentResourceAssemblyName = ComponentResourceAssembly.GetName());
        private AssemblyName _ComponentResourceAssemblyName;

        private IEnumerable<string> ComponentResourceAssemblyResourceNames =>
            _ComponentResourceAssemblyResourceNames ?? (
            _ComponentResourceAssemblyResourceNames = ComponentResourceAssembly.GetManifestResourceNames());
        private IEnumerable<string> _ComponentResourceAssemblyResourceNames;

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

        private Stream StreamCulture(string extension) {
            var resourceNames = ComponentResourceAssemblyResourceNames;
            var cultureChecks = CultureChecks();
            foreach (var culture in cultureChecks) {
                foreach (var resourceName in resourceNames) {
                    if (resourceName.EndsWith($".Resource.Culture.{culture}{extension}")) {
                        return ComponentResourceAssembly.GetManifestResourceStream(resourceName);
                    }
                }
            }
            return null;
        }

        private async Task<string> ReadCulture(string extension, CancellationToken token) {
            var stream = StreamCulture(extension);
            if (stream != null) {
                await using (stream) {
                    using (var reader = new StreamReader(stream)) {
                        return await reader.ReadToEndAsync(token).ConfigureAwait(false);
                    }
                }
            }
            return null;
        }

        private async Task<IEnumerable<KeyValuePair<string, string>>> SplitCulture(string extension, CancellationToken token) {
            var read = await ReadCulture(extension, token).ConfigureAwait(false);
            var text = read?.Trim() ?? "";
            var lines = text.Split('\r', '\n');
            var items = lines
                .Select(line => line.Split('=', 2))
                .Where(pair => pair.Length == 2)
                .Select(pair => KeyValuePair.Create(key: pair[0].Trim(), value: pair[1].Trim()));
            return items;
        }

        internal IEnumerable<ResourceDictionary> ResourceDictionaries {
            get {
                var assembly = ReferencedAssembly;
                var resource = SourceResource($"pack://application:,,,/{assembly};component/Resource/Resource.xaml");
                if (resource != null) {
                    yield return resource;
                }
                var cultureChecks = CultureChecks();
                foreach (var culture in cultureChecks) {
                    var cultureResource = SourceResource($"pack://application:,,,/{assembly};component/Resource/Culture/{culture}.xaml");
                    if (cultureResource != null) {
                        yield return cultureResource;
                        break;
                    }
                }
            }
        }

        internal object ComponentResource { get; }

        internal AppComponent(object componentResource) {
            ComponentResource = componentResource ?? throw new ArgumentNullException(nameof(componentResource));
        }

        internal async Task<IReadOnlyDictionary<string, string>> Translate(CancellationToken token) {
            if (_Translate == null) {
                var dict = new Dictionary<string, string>();
                var split = await SplitCulture("", token).ConfigureAwait(false);
                foreach (var pair in split) {
                    dict[pair.Key] = pair.Value;
                }
                var resourceNames = ComponentResourceAssemblyResourceNames;
                var cultureChecks = CultureChecks();
                foreach (var culture in cultureChecks) {
                    var pttrn = $@"(?<=.+\.Resource\.Culture\.{culture}\.s\.)(.+)";
                    var regex = new Regex(pttrn);
                    foreach (var resourceName in resourceNames) {
                        var match = regex.Match(resourceName);
                        if (match.Success) {
                            await using (var stream = ComponentResourceAssembly.GetManifestResourceStream(resourceName)) {
                                using (var reader = new StreamReader(stream)) {
                                    var key = match.Value;
                                    var value = await reader.ReadToEndAsync(token).ConfigureAwait(false);
                                    dict[key] = value;
                                }
                            }
                        }
                    }
                }
                _Translate = dict;
            }
            return _Translate;
        }
        private IReadOnlyDictionary<string, string> _Translate;

        internal async Task<IReadOnlyDictionary<string, IEnumerable<string>>> Alias(CancellationToken token) {
            if (_Alias == null) {
                var dict = new Dictionary<string, IEnumerable<string>>();
                var split = await SplitCulture(".alias", token).ConfigureAwait(false);
                foreach (var pair in split) {
                    if (dict.TryGetValue(pair.Key, out var value) == false) {
                        dict[pair.Key] = value = new HashSet<string>();
                    }
                    var
                    list = (HashSet<string>)value;
                    list.Add(pair.Value);
                }
                _Alias = dict;
            }
            return _Alias;
        }
        private IReadOnlyDictionary<string, IEnumerable<string>> _Alias;

        public string ReferencedAssembly => _ReferencedAssembly ??=
            ComponentResourceAssemblyName.Name;
        private string _ReferencedAssembly;

        public static AppComponentDataTemplateSelectorCollection DataTemplateSelector => _DataTemplateSelector ??=
            new AppComponentDataTemplateSelectorCollection();
        private static AppComponentDataTemplateSelectorCollection _DataTemplateSelector;
    }
}
