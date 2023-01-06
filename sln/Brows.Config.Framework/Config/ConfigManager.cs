using Domore.Logs;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    using Threading.Tasks;

    internal class ConfigManager : IConfigManager {
        public string ID { get; }

        public ConfigManager(string id) {
            ID = id;
        }
    }

    internal class ConfigManager<TConfig> : ConfigManager, IConfigManager<TConfig> where TConfig : INotifyPropertyChanged, new() {
        private static readonly ILog Log = Logging.For(typeof(ConfigManager<TConfig>));

        private TConfig Config;
        private Task<object> ConfigTask;
        private Guid ChangeState = Guid.NewGuid();
        private readonly int ChangeDelay = 1000;
        private readonly object ConfigLocker = new object();

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<ConfigManager<TConfig>>());
        private TaskHandler _TaskHandler;

        private ConfigStore Store =>
            _Store ?? (
            _Store = new ConfigStore(typeof(TConfig).Name, ID));
        private ConfigStore _Store;

        private void Config_PropertyChanged(object sender, EventArgs e) {
            var state = ChangeState = Guid.NewGuid();
            TaskHandler.Begin(async cancellationToken => {
                await Task.Delay(ChangeDelay, cancellationToken);
                if (ChangeState == state) {
                    if (Log.Info()) {
                        Log.Info(nameof(Store.Save));
                    }
                    Config = (TConfig)await Store.Save(Config, cancellationToken);
                    Config.PropertyChanged -= Config_PropertyChanged;
                    Config.PropertyChanged += Config_PropertyChanged;
                }
            });
        }

        public ConfigManager(string id) : base(id) {
        }

        public TConfig Get() {
            return Config;
        }

        public async Task<TConfig> Load(CancellationToken cancellationToken) {
            if (Config == null) {
                if (ConfigTask == null) {
                    if (Log.Info()) {
                        Log.Info(nameof(Store.Load));
                    }
                    ConfigTask = Store.Load(new TConfig(), cancellationToken);
                }
                Config = (TConfig)await ConfigTask;
                Config.PropertyChanged -= Config_PropertyChanged;
                Config.PropertyChanged += Config_PropertyChanged;
            }
            return Config;
        }
    }
}
