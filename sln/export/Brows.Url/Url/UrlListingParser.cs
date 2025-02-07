using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Url {
    internal abstract class UrlListingParser<T> {
        protected abstract IAsyncEnumerable<T> ParseLines(IAsyncEnumerable<string> lines, CancellationToken token);

        public IAsyncEnumerable<T> ParseContents(IAsyncEnumerable<string> contents, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(contents);
            async IAsyncEnumerable<string> lines() {
                var sb = new StringBuilder();
                await foreach (var content in contents.WithCancellation(token).ConfigureAwait(false)) {
                    sb.Append(content);
                    var i = 0;
                    var j = 0;
                    var p = default(char);
                    for (; i < sb.Length; i++) {
                        var c = sb[i];
                        if (c == '\n') {
                            var n = p == '\r' ? i - 1 : i;
                            var line = sb.ToString(j, n - j);
                            j = i + 1;
                            yield return line;
                        }
                        p = c;
                    }
                    sb.Remove(0, j);
                }
                if (sb.Length > 0) {
                    yield return sb.ToString();
                }
            }
            return ParseLines(lines(), token);
        }
    }
}
