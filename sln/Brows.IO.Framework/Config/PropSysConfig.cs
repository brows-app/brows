using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    internal class PropSysConfig {
        private PropSys PropSys;

        private IConfig<PropSys> Config =>
            _Config ?? (
            _Config = Configure.File<PropSys>());
        private IConfig<PropSys> _Config;

        private PropSysConfig() {
        }

        private void Load(PropSys propertySystem) {
            if (null == propertySystem) throw new ArgumentNullException(nameof(propertySystem));
            Keys = new HashSet<string>(propertySystem.Prop.Select(p => p.Key));
            Properties = propertySystem.Prop.Select(p => new FilePropertyData(p.Key, p.Width ?? 100)).ToList();
        }

        public IReadOnlySet<string> Keys {
            get => _Keys ?? (_Keys = new HashSet<string>());
            private set => _Keys = value;
        }
        private IReadOnlySet<string> _Keys;

        public IReadOnlyList<FilePropertyData> Properties {
            get => _Properties ?? (_Properties = Array.Empty<FilePropertyData>());
            private set => _Properties = value;
        }
        private IReadOnlyList<FilePropertyData> _Properties;

        public static readonly PropSysConfig Instance = new PropSysConfig();

        public async Task Init(CancellationToken cancellationToken) {
            if (PropSys == null) {
                PropSys = await Config.Load(cancellationToken);
                Load(PropSys);
            }
        }

        public IEnumerable<IEntryData> For(FileSystemInfoWrapper wrapper, CancellationToken cancellationToken) {
            var propertySystem = Config.Loaded;
            if (propertySystem != null) {
                return propertySystem.Prop
                    .Select(p => new FilePropertyData(p.Key, p.Width ?? 100).Implement(wrapper, cancellationToken))
                    .ToList();
            }
            return Array.Empty<IEntryData>();
        }
    }
}
