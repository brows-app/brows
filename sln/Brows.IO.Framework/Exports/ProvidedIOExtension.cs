using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Exports {
    public static class ProvidedIOExtension {
        public static IReadOnlyList<string> Files(this IEnumerable<IProvidedIO> providedIO) {
            return providedIO
                .StreamSets()
                .SelectMany(s => s.FileSource())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct()
                .ToList();
        }

        public static IReadOnlyList<IEntryStreamSet> StreamSets(this IEnumerable<IProvidedIO> providedIO) {
            if (null == providedIO) throw new ArgumentNullException(nameof(providedIO));
            return providedIO
                .SelectMany(io => io.StreamSets ?? Array.Empty<IEntryStreamSet>())
                .Where(streamSet => streamSet is not null)
                .ToList();
        }
    }
}
