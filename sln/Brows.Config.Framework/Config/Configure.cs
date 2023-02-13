using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Brows.Config {
    public static class Configure {
        private static readonly Dictionary<Type, ConfigFileManager> ConfFile = new();
        private static readonly Dictionary<Type, Dictionary<string, ConfigDataManager>> DataFile = new();

        public static string Root =>
            ConfigPath.DataRoot;

        public static IConfig<TConfig> Data<TConfig>(string id = null) where TConfig : class, INotifyPropertyChanged, new() {
            var typ = typeof(TConfig);
            var key = id ?? typ.Name;
            if (DataFile.TryGetValue(typ, out var dataFile) == false) {
                DataFile[typ] = dataFile = new Dictionary<string, ConfigDataManager>();
            }
            if (dataFile.TryGetValue(key, out var data) == false) {
                dataFile[key] = data = new ConfigDataManager.Of<TConfig>(key);
            }
            return (IConfig<TConfig>)data;
        }

        public static IConfig<TConfig> File<TConfig>() where TConfig : class, new() {
            var type = typeof(TConfig);
            if (ConfFile.TryGetValue(type, out var conf) == false) {
                ConfFile[type] = conf = new ConfigFileManager.Of<TConfig>();
            }
            return (IConfig<TConfig>)conf;
        }
    }
}
