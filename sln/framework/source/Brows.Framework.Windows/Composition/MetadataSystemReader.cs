using Brows.Operations;
using Brows.Threading;
using Brows.Win32;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Composition;

internal sealed class MetadataSystemReader : IMetadataSystemReader {
    [ImportRequired]
    internal ISTAThreadPoolProvider ThreadPoolProvider { get; set; }

    public async Task<bool> Work(ICollection<IMetadataDefinition> definitions,
                                 string file,
                                 IOperationProgress progress,
                                 CancellationToken token) {
        if (definitions is null) {
            return false;
        }
        var threadPool = ThreadPoolProvider?.GetThreadPool(this);
        if (threadPool is null) {
            throw new InvalidOperationException("Unable to get a thread pool!");
        }
        var list = await threadPool.Work(nameof(MetadataSystemReader), cancellationToken: token, work: () => {
            var list = new List<MetadataDefinition>();
            var items = string.IsNullOrWhiteSpace(file)
                ? PropertySystem.EnumeratePropertyDescriptions()
                : PropertySystem.EnumeratePropertyDescriptions(file, throwOnError: false);
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
