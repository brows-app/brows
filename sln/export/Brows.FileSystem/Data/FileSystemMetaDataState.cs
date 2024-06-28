using Brows.Exports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Data {
    internal sealed class FileSystemMetaDataState {
        private readonly Dictionary<string, File> FileSet = new();

        private IMetadataSystemReader Service { get; }

        private FileSystemMetaDataState(IMetadataSystemReader service, IReadOnlyDictionary<string, FileSystemMetaData> system) {
            Service = service ?? throw new ArgumentNullException(nameof(service));
            System = system ?? throw new ArgumentNullException(nameof(system));
        }

        public IReadOnlyDictionary<string, FileSystemMetaData> System { get; }

        public IReadOnlyCollection<IEntryDataDefinition> DataDefinition =>
            _DataDefinition ?? (
            _DataDefinition = System.Values.ToList());
        private IReadOnlyCollection<IEntryDataDefinition> _DataDefinition;

        public static async Task<FileSystemMetaDataState> Load(IMetadataSystemReader service, CancellationToken token) {
            if (null == service) throw new ArgumentNullException(nameof(service));
            var list = new List<IMetadataDefinition>();
            var work = await service.Work(list, null, null, token).ConfigureAwait(false);
            var data = work == false
                ? new()
                : list
                    .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                    .DistinctBy(item => item.Key)
                    .ToDictionary(item => item.Key, item => new FileSystemMetaData(item));
            return new FileSystemMetaDataState(service, data);
        }

        public async Task<File> Load(string file, object state, CancellationToken token) {
            if (FileSet.TryGetValue(file, out var metadata) == false || metadata.State != state) {
                FileSet[file] = metadata = await File
                    .Load(file, state, Service, token)
                    .ConfigureAwait(false);
            }
            return metadata;
        }

        public sealed class File {
            private File(string path, object state, IReadOnlyList<IMetadataDefinition> definitions) {
                Path = path;
                State = state;
                Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            }

            public IReadOnlySet<string> Keys =>
                _Keys ?? (
                _Keys = new HashSet<string>(Definitions.Select(d => d.Key)));
            private IReadOnlySet<string> _Keys;

            public string Path { get; }
            public object State { get; }
            public IReadOnlyList<IMetadataDefinition> Definitions { get; }

            public static async Task<File> Load(string path, object state, IMetadataSystemReader reader, CancellationToken token) {
                if (null == reader) throw new ArgumentNullException(nameof(reader));
                var list = new List<IMetadataDefinition>();
                var work = await reader
                    .Work(list, path, null, token)
                    .ConfigureAwait(false);
                return new File(path, state, list);
            }
        }
    }
}
