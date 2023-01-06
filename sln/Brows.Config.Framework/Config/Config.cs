using System;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace Brows.Config {
    public static class Config {
        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, ConfigManager>> Manager = new();

        public static string Root =>
            ConfigPath.Root;

        public static IConfigManager<TConfig> Manage<TConfig>(string id = null) where TConfig : INotifyPropertyChanged, new() {
            var typ = typeof(TConfig);
            var key = id ?? typ.Name;
            if (Manager.TryGetValue(typ, out var manager) == false) {
                Manager[typ] = manager = new ConcurrentDictionary<string, ConfigManager>();
            }
            if (manager.TryGetValue(key, out var managed) == false) {
                manager[key] = managed = new ConfigManager<TConfig>(key);
            }
            return (IConfigManager<TConfig>)managed;
        }
    }
}
