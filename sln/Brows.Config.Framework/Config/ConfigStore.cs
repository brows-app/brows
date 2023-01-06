using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    internal class ConfigStore {
        private ConfigFile ConfigFile =>
            _ConfigFile ?? (
            _ConfigFile = new ConfigFile());
        private ConfigFile _ConfigFile;

        private ConfigFilePayload Payload(object data) {
            return new ConfigFilePayload {
                Data = data,
                ID = ID,
                Type = Type
            };
        }

        public string ID { get; }
        public string Type { get; }

        public ConfigStore(string type, string id) {
            ID = id;
            Type = type;
        }

        public async Task<object> Save(object data, CancellationToken cancellationToken) {
            var save = await ConfigFile.Save(Payload(data), cancellationToken);
            return save;
        }

        public async Task<object> Load(object data, CancellationToken cancellationToken) {
            var load = await ConfigFile.Load(Payload(data), cancellationToken);
            return load;
        }
    }
}
