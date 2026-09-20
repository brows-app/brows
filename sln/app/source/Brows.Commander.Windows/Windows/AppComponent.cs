using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Brows.Windows;

public sealed class AppComponent {
    private AssemblyName AssemblyName => field ??= Assembly.GetName();
    private IReadOnlyList<string> AssemblyResourceNames => field ??= Assembly.GetManifestResourceNames();

    private static ResourceDictionary SourceResource(string source) {
        try {
            return new ResourceDictionary { Source = new Uri(source) };
        }
        catch {
            return null;
        }
    }

    private static IEnumerable<string> CultureChecks() {
        var culture = Thread.CurrentThread?.CurrentUICulture;
        var cultureName = culture?.Name;
        if (cultureName is not null) {
            yield return cultureName;
        }
        var parentName = culture?.Parent?.Name;
        if (parentName is not null && parentName != cultureName) {
            yield return parentName;
        }
        yield return "_";
    }

    private Stream StreamCulture(string extension) {
        var resourceNames = AssemblyResourceNames;
        var cultureChecks = CultureChecks();
        foreach (var culture in cultureChecks) {
            foreach (var resourceName in resourceNames) {
                if (resourceName.EndsWith($".Resource.Culture.{culture}{extension}")) {
                    return Assembly.GetManifestResourceStream(resourceName);
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
                    return await reader.ReadToEndAsync(token);
                }
            }
        }
        return null;
    }

    private async Task<IEnumerable<KeyValuePair<string, string>>> SplitCulture(string extension, CancellationToken token) {
        var read = await ReadCulture(extension, token);
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

    internal Assembly Assembly { get; }

    internal AppComponent(Assembly assembly) {
        ArgumentNullException.ThrowIfNull(assembly);
        Assembly = assembly;
    }

    internal async Task<IReadOnlyDictionary<string, string>> Translate(CancellationToken token) {
        if (_Translate == null) {
            var dict = new Dictionary<string, string>();
            var split = await SplitCulture("", token);
            foreach (var pair in split) {
                dict[pair.Key] = pair.Value;
            }
            var resourceNames = AssemblyResourceNames;
            var cultureChecks = CultureChecks();
            foreach (var culture in cultureChecks) {
                var pttrn = $@"(?<=.+\.Resource\.Culture\.{culture}\.s\.)(.+)";
                var regex = new Regex(pttrn);
                foreach (var resourceName in resourceNames) {
                    var match = regex.Match(resourceName);
                    if (match.Success) {
                        await using (var stream = Assembly.GetManifestResourceStream(resourceName)) {
                            using (var reader = new StreamReader(stream)) {
                                var key = match.Value;
                                var value = await reader.ReadToEndAsync(token);
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
            var split = await SplitCulture(".alias", token);
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

    public string ReferencedAssembly => field ??= AssemblyName.Name;

    public static AppComponentDataTemplateSelectorCollection DataTemplateSelector => field ??=
        new AppComponentDataTemplateSelectorCollection();
}
