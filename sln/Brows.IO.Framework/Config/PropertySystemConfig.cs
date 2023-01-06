using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    internal class PropertySystemConfig {
        private PropertySystem PropertySystem;

        private IConfigManager<PropertySystem> Manager =>
            _Manager ?? (
            _Manager = Config.Manage<PropertySystem>());
        private IConfigManager<PropertySystem> _Manager;

        private PropertySystemConfig() {
        }

        private void PropertySystem_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            var data = sender as PropertySystem;
            if (data != null) {
                Load(data);
            }
        }

        private void Load(PropertySystem propertySystem) {
            if (null == propertySystem) throw new ArgumentNullException(nameof(propertySystem));
            Keys = new HashSet<string>(propertySystem.Properties.Select(p => p.Key));
            Properties = propertySystem.Properties.Select(p => new FilePropertyData(p.Key, p.Width ?? 100)).ToList();
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

        public static readonly PropertySystemConfig Instance = new PropertySystemConfig();

        public async Task Init(CancellationToken cancellationToken) {
            if (PropertySystem == null) {
                PropertySystem = await Manager.Load(cancellationToken);
                PropertySystem.PropertyChanged += PropertySystem_PropertyChanged;
                Load(PropertySystem);
            }
        }

        public IEnumerable<IEntryData> For(FileSystemInfoWrapper wrapper, CancellationToken cancellationToken) {
            var propertySystem = Manager.Get();
            if (propertySystem != null) {
                return propertySystem.Properties
                    .Select(p => new FilePropertyData(p.Key, p.Width ?? 100).Implement(wrapper, cancellationToken))
                    .ToList();
            }
            return Array.Empty<IEntryData>();
        }
    }
}
