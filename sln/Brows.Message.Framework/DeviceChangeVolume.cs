using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public class DeviceChangeVolume : DeviceChangeInfo {
        public DeviceChangeVolumeFlag Flag { get; init; }
        public IReadOnlySet<char> Drive { get; init; }

        public sealed override string ToString() {
            return $"{Type} Flag: [{Flag}], Drives: [{string.Join(",", Drive)}]";
        }

        public async Task<bool?> Affects(FileSystemInfo fileSystem, CancellationToken token) {
            if (fileSystem == null) {
                return false;
            }
            var drive = Drive;
            if (drive == null) {
                return false;
            }
            var path = fileSystem.FullName;
            if (Path.IsPathFullyQualified(path) == false) {
                return null;
            }
            if (drive.Any(d => path.StartsWith($"{d}:", StringComparison.OrdinalIgnoreCase))) {
                return true;
            }
            return await Task.FromResult(false);
        }
    }
}
