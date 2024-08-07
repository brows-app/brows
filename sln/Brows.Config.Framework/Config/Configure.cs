﻿using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Brows.Config {
    public static class Configure {
        private static readonly Dictionary<Type, ConfigFileManager> ConfFile = [];
        private static readonly Dictionary<Type, Dictionary<string, ConfigDataManager>> DataFile = [];

        public static string Root =>
            ConfigPath.DataRoot;

        public static IConfig<TConfig> Data<TConfig>(string id = null) where TConfig : class, INotifyPropertyChanged, new() {
            var typ = typeof(TConfig);
            var key = id ?? typ.Name;
            var data = default(ConfigDataManager);
            var dataFile = default(Dictionary<string, ConfigDataManager>);
            lock (DataFile) {
                if (DataFile.TryGetValue(typ, out dataFile) == false) {
                    DataFile[typ] = dataFile = [];
                }
            }
            lock (dataFile) {
                if (dataFile.TryGetValue(key, out data) == false) {
                    dataFile[key] = data = new ConfigDataManager.Of<TConfig>(key);
                }
            }
            return (IConfig<TConfig>)data;
        }

        public static IConfig<TConfig> File<TConfig>() where TConfig : class, new() {
            var type = typeof(TConfig);
            lock (ConfFile) {
                if (ConfFile.TryGetValue(type, out var conf) == false) {
                    ConfFile[type] = conf = new ConfigFileManager.Of<TConfig>();
                }
                return (IConfig<TConfig>)conf;
            }
        }
    }
}
