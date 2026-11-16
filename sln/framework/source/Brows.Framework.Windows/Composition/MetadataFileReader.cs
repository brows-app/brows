using Brows.Operations;
using Brows.Threading;
using Brows.Win32;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Composition;

internal sealed class MetadataFileReader : IMetadataFileReader {
    [ImportRequired]
    internal ISTAThreadPoolProvider ThreadPoolProvider { get; set; }

    public async Task<bool> Work(string file,
                                 IDictionary<IMetadataDefinition, IMetadataValue> values,
                                 IOperationProgress progress,
                                 CancellationToken token) {
        var keys = values?.Keys;
        if (keys == null) {
            return false;
        }
        var metadata = keys.OfType<MetadataDefinition>().ToList();
        if (metadata.Count == 0) {
            return false;
        }
        var threadPool = ThreadPoolProvider?.GetThreadPool(this);
        if (threadPool is null) {
            throw new InvalidOperationException("Unable to get a thread pool!");
        }
        var propertyDescriptions = metadata.Select(d => d.PropertyDescription).ToList();
        var dict = await threadPool.Work(nameof(MetadataFileReader), cancellationToken: token, work: () => {
            var dict = new Dictionary<PropertyDescription, PropertyValue>();
            var items = PropertySystem.EnumeratePropertyValues(file, propertyDescriptions, throwOnError: false);
            foreach (var item in items) {
                token.ThrowIfCancellationRequested();
                dict[item.Description] = item;
            }
            return dict;
        });
        foreach (var key in metadata) {
            if (dict.TryGetValue(key.PropertyDescription, out var value)) {
                values[key] = new MetadataValue {
                    Display = value.Display,
                    Object = value.Object
                };
            }
        }
        return true;
    }
}
