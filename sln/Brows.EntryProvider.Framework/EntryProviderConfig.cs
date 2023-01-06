using Domore.Conf;
using System;
using System.Collections.Generic;

namespace Brows {
    internal sealed class EntryProviderConfig : EntryConfig {
        private static readonly Dictionary<Type, object> Config = new();

        private EntryProviderConfig() {
        }

        public static readonly EntryConfig Instance = new EntryProviderConfig();

        public sealed override T Configure<T>() {
            var type = typeof(T);
            var target = default(object);
            lock (Config) {
                if (Config.TryGetValue(type, out target) == false) {
                    Config[type] = target = Conf.Configure(new T());
                }
            }
            return (T)target;
        }
    }
}
