using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Brows.Globalization {
    internal class Translation : Notifier, ITranslation {
        private static async Task<string> Read(string rsrc) {
            var contents = "";
            var assembly = Assembly.GetExecutingAssembly();
            var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith(rsrc));
            await using (var stream = assembly.GetManifestResourceStream(resource)) {
                using (var reader = new StreamReader(stream)) {
                    contents = await reader.ReadToEndAsync();
                }
            }
            return contents;
        }

        private static async Task<IEnumerable<KeyValuePair<string, string>>> Split(string rsrc) {
            var contents = await Read(rsrc);
            var lines = contents.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var items = lines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries))
                .Where(item => item.Length == 2)
                .Select(items => new KeyValuePair<string, string>(key: items[0].Trim(), value: items[1].Trim()));
            return items;
        }

        private async Task LoadAsync(string culture) {
            var translations = await Split(culture);
            foreach (var t in translations) {
                Translator[culture].Value(t.Key, t.Value);
            }

            var alias = await Split(culture + ".alias");
            var aliases = alias.GroupBy(pair => pair.Key, pair => pair.Value);
            foreach (var a in aliases) {
                Translator[culture].Alias(a.Key, a.ToArray());
            }
        }

        private void Load(string culture) {
            Task.Run(() => LoadAsync(culture)).Wait();
        }

        public string this[string key] => Value(key);

        public string Dialect {
            get {
                if (_Dialect == null) {
                    _Dialect = CultureInfo.CurrentCulture.Name;
                    Load(_Dialect);
                }
                return _Dialect;
            }
            set {
                if (_Dialect != value) {
                    _Dialect = value;
                    Load(_Dialect);
                    NotifyPropertyChanged();
                }
            }
        }
        private string _Dialect;

        public Translator Translator {
            get => _Translator ?? (_Translator = new Translator());
            set {
                if (_Translator != value) {
                    _Translator = value;
                    NotifyPropertyChanged();
                }
            }
        }
        private Translator _Translator;

        public string Value(string key) {
            return Translator[Dialect].Value(key);
        }

        public string[] Alias(string key) {
            return Translator[Dialect].Alias(key);
        }
    }
}
