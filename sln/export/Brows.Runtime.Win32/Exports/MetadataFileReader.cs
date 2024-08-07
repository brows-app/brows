﻿using Domore.Runtime.Win32;
using Domore.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class MetadataFileReader : IMetadataFileReader {
        private readonly STAThreadPool ThreadPool = Win32ThreadPool.Common;

        public async Task<bool> Work(string file, IDictionary<IMetadataDefinition, IMetadataValue> values, IOperationProgress progress, CancellationToken token) {
            var keys = values?.Keys;
            if (keys == null) {
                return false;
            }
            var metadata = keys.OfType<MetadataDefinition>().ToList();
            if (metadata.Count == 0) {
                return false;
            }
            var propertyDescriptions = metadata.Select(d => d.PropertyDescription).ToList();
            var dict = await ThreadPool.Work(nameof(MetadataFileReader), cancellationToken: token, work: () => {
                var dict = new Dictionary<PropertyDescription, PropertyValue>();
                var items = PropertySystem.EnumeratePropertyValues(file, propertyDescriptions, throwOnError: false);
                foreach (var item in items) {
                    token.ThrowIfCancellationRequested();
                    dict[item.Description] = item;
                }
                return dict;
            }).ConfigureAwait(false);
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
}
