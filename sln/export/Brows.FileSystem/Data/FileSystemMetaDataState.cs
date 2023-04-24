using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Data {
    using Exports;

    internal sealed class FileSystemMetaDataState {
        private readonly Dictionary<string, IReadOnlySet<string>> FileSet = new(StringComparer.OrdinalIgnoreCase);

        private IMetadataSystemReader Service { get; }

        private FileSystemMetaDataState(IMetadataSystemReader service, IReadOnlyDictionary<string, FileSystemMetaData> system) {
            Service = service ?? throw new ArgumentNullException(nameof(service));
            System = system ?? throw new ArgumentNullException(nameof(system));
        }

        public IReadOnlyDictionary<string, IReadOnlySet<string>> File =>
            FileSet;

        public IReadOnlyDictionary<string, FileSystemMetaData> System { get; }

        public IReadOnlyCollection<IEntryDataDefinition> DataDefinition =>
            _DataDefinition ?? (
            _DataDefinition = System.Values.ToList());
        private IReadOnlyCollection<IEntryDataDefinition> _DataDefinition;

        public static async Task<FileSystemMetaDataState> Load(IMetadataSystemReader service, CancellationToken token) {
            if (null == service) throw new ArgumentNullException(nameof(service));
            var list = new List<IMetadataDefinition>();
            var work = await service.Work(list, null, null, token);
            var data = work == false
                ? new()
                : list
                    .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                    .DistinctBy(item => item.Key)
                    .ToDictionary(item => item.Key, item => new FileSystemMetaData(item));
            return new FileSystemMetaDataState(service, data);
        }

        public async Task<string> Ready(string file, CancellationToken token) {
            var key = Path.GetExtension(file);
            if (FileSet.TryGetValue(key, out var set)) {
                return key;
            }
            lock (FileSet) {
                if (FileSet.TryGetValue(key, out set)) {
                    return key;
                }
                FileSet[key] = set = new HashSet<string>();
            }
            var list = new List<IMetadataDefinition>();
            var work = await Service.Work(list, file, null, token);
            if (work) {
                list.ForEach(item => ((HashSet<string>)set).Add(item.Key));
            }
            return key;
        }
    }
}
