using Domore.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Windows {
    public sealed class AppComponentTranslation : ITranslation {
        private readonly ConcurrentDictionary<string, string> Value = new();
        private readonly ConcurrentDictionary<string, IReadOnlySet<string>> Alias = new();

        private TaskCache<AppComponentTranslation> Load => _Load ??=
            new(async token => {
                var comps = Components;
                var tasks = new List<Task>();
                foreach (var item in comps) {
                    tasks.Add(item.Alias(token));
                    tasks.Add(item.Translate(token));
                }
                await Task.WhenAll(tasks);
                foreach (var item in comps) {
                    var translate = await item.Translate(token);
                    foreach (var t in translate) {
                        Value[t.Key] = t.Value;
                    }
                    var alias = await item.Alias(token);
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
                return this;
            });
        private TaskCache<AppComponentTranslation> _Load;

        public object this[string key] =>
            Value.TryGetValue(key, out var value)
                ? value
                : null;

        private AppComponentCollection Components { get; }

        private AppComponentTranslation(AppComponentCollection collection) {
            Components = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        public static async Task<AppComponentTranslation> Ready(AppComponentCollection components, CancellationToken token) {
            var
            @this = new AppComponentTranslation(components);
            @this = await @this.Load.Ready(token);
            return @this;
        }

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
    }
}
