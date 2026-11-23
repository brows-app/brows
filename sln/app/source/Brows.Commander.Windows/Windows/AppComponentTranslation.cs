using Brows.Composition;
using Brows.Localization;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Windows;

public sealed class AppComponentTranslation : ITranslationExport, IExportAndInit {
    private readonly ConcurrentDictionary<string, string> Value = new();
    private readonly ConcurrentDictionary<string, IReadOnlySet<string>> Alias = new();

    public object this[string key] =>
        Value.TryGetValue(key, out var value)
            ? value
            : null;

    string ITranslation.Value(string key) {
        return Value.TryGetValue(key, out var value)
            ? value
            : null;
    }

    string[] ITranslation.Alias(string key) {
        return Alias.TryGetValue(key, out var value)
            ? value.ToArray()
            : Array.Empty<string>();
    }

    async Task IExportAndInit.Init(IExportContext context, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(context);
        var components = AppComponentCollection.From(context);
        var tasks = new List<Task>();
        foreach (var item in components) {
            tasks.Add(item.Alias(token));
            tasks.Add(item.Translate(token));
        }
        await Task
            .WhenAll(tasks);
        foreach (var item in components) {
            var translate = await item
                .Translate(token);
            foreach (var t in translate) {
                Value[t.Key] = t.Value;
            }
            var alias = await item
                .Alias(token);
            foreach (var a in alias) {
                if (Alias.TryGetValue(a.Key, out var value) == false) {
                    Alias[a.Key] = value = new HashSet<string>();
                }
                var list = (ICollection<string>)value;
                foreach (var v in a.Value) {
                    list.Add(v);
                }
            }
        }
    }
}
