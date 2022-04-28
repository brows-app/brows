using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using IO;

    public class PathBookmark : IBookmark {
        public Task<bool> Exists(string value, CancellationToken cancellationToken) {
            return DirectoryAsync.Exists(value, cancellationToken);
        }

        public Task<KeyValuePair<string, string>> MakeFrom(string value, IEnumerable<KeyValuePair<string, string>> existing, CancellationToken cancellationToken) {
            if (null == value) throw new ArgumentNullException(nameof(value));
            if (null == existing) throw new ArgumentNullException(nameof(existing));
            var sep = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            var parts = value
                .Split(
                    sep,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Reverse()
                .ToList();
            var items = existing;
            var take = 1;
            var key = default(string);
            for (; ; ) {
                var k = string.Join(Path.DirectorySeparatorChar, parts.Take(take).Reverse());
                if (items.Any(item => item.Key == k)) {
                    take++;
                }
                else {
                    key = k;
                    break;
                }
            }
            return Task.FromResult(new KeyValuePair<string, string>(key ?? value, value));
        }
    }
}
