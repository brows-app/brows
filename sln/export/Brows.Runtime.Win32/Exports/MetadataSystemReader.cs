using Domore.Runtime.Win32;
using Domore.Threading;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class MetadataSystemReader : IMetadataSystemReader {
        private readonly STAThreadPool ThreadPool = Win32ThreadPool.Common;

        public async Task<bool> Work(ICollection<IMetadataDefinition> definitions, string file, IOperationProgress progress, CancellationToken token) {
            if (definitions is null) {
                return false;
            }
            var list = await ThreadPool.Work(nameof(MetadataSystemReader), cancellationToken: token, work: () => {
                var list = new List<MetadataDefinition>();
                var items = string.IsNullOrWhiteSpace(file)
                    ? PropertySystem.EnumeratePropertyDescriptions()
                    : PropertySystem.EnumeratePropertyDescriptions(file);
                foreach (var item in items) {
                    token.ThrowIfCancellationRequested();
                    list.Add(new MetadataDefinition(item));
                }
                return list;
            });
            foreach (var item in list) {
                definitions.Add(item);
            }
            return true;
        }
    }
}
