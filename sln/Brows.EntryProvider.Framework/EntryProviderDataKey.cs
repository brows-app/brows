using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    using Translation;

    internal class EntryProviderDataKey {
        private ITranslation Translate =>
            Global.Translation;

        public IReadOnlySet<string> Keys { get; }

        public EntryProviderDataKey(IReadOnlySet<string> keys) {
            Keys = keys ?? throw new ArgumentNullException(nameof(keys));
        }

        public IReadOnlyDictionary<string, IReadOnlySet<string>> Alias {
            get {
                if (_Alias == null) {
                    var dict = new Dictionary<string, IReadOnlySet<string>>(StringComparer.CurrentCultureIgnoreCase);
                    var keys = Keys;
                    foreach (var key in keys) {
                        var set = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                        var keyAliases = Translate.Alias(key);
                        if (keyAliases.Length == 0) {
                            set.Add(key);
                        }
                        else {
                            foreach (var keyAlias in keyAliases) {
                                set.Add(keyAlias);
                            }
                        }
                        dict.Add(key, set);
                    }
                    _Alias = dict;
                }
                return _Alias;
            }
        }
        private IReadOnlyDictionary<string, IReadOnlySet<string>> _Alias;

        public IEnumerable<string> Lookup(string[] aliases) {
            foreach (var key in Keys) {
                var alias = Alias[key];
                var aliased = aliases.Any(a => string.Equals(a, key, StringComparison.CurrentCultureIgnoreCase) || alias.Contains(a));
                if (aliased) {
                    yield return key;
                }
            }
        }

        public string Lookup(string alias) {
            foreach (var item in Lookup(new[] { alias })) {
                return item;
            }
            return null;
        }

        public IReadOnlySet<string> Possible(string part) {
            var set = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            var keys = Keys;
            foreach (var key in keys) {
                var alias = Alias[key];
                var aliased = alias.Any(a => a.Contains(part, StringComparison.CurrentCultureIgnoreCase));
                if (aliased) {
                    set.Add(key);
                }
            }
            return set;
        }
    }
}
