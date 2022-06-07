using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Windows {
    using Translation;

    internal class AppComponentTranslation : ITranslation {
        private bool Loaded;
        private readonly Dictionary<string, string> Value = new Dictionary<string, string>();
        private readonly Dictionary<string, IEnumerable<string>> Alias = new Dictionary<string, IEnumerable<string>>();

        private void Load() {
            if (Loaded == false) {
                Loaded = true;
                Task.Run(async () => await Load(CancellationToken.None)).Wait();
            }
        }

        private async Task Load(CancellationToken cancellationToken) {
            var coll = Collection;
            var tasks = new List<Task>();
            foreach (var item in coll) {
                tasks.Add(item.Alias(cancellationToken));
                tasks.Add(item.Translate(cancellationToken));
            }
            await Task.WhenAll(tasks);
            foreach (var item in coll) {
                var translate = await item.Translate(cancellationToken);
                foreach (var t in translate) {
                    Value[t.Key] = t.Value;
                }
                var alias = await item.Alias(cancellationToken);
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

        public AppComponentCollection Collection { get; }

        public AppComponentTranslation(AppComponentCollection collection) {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        string ITranslation.Value(string key) {
            Load();
            return Value.TryGetValue(key, out var value)
                ? value
                : null;
        }

        string[] ITranslation.Alias(string key) {
            Load();
            return Alias.TryGetValue(key, out var value)
                ? value.ToArray()
                : Array.Empty<string>();
        }
    }
}
