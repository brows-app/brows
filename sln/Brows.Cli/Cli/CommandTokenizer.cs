using System.Collections.Generic;
using System.Text;

namespace Brows.Cli {
    internal class CommandTokenizer {
        public IEnumerable<CommandToken> Tokenize(string s) {
            if (s == null) yield break;
            var q = default(char?);
            var b = new StringBuilder();
            foreach (var c in s) {
                if (char.IsWhiteSpace(c)) {
                    if (q == null) {
                        if (b.Length > 0) {
                            yield return new CommandToken(b.ToString());
                            b = new StringBuilder();
                        }
                    }
                    else {
                        b.Append(c);
                    }
                }
                else {
                    b.Append(c);
                    if (c == q) {
                        q = null;
                        yield return new CommandToken(b.ToString());
                        b = new StringBuilder();
                    }
                    else {
                        if (q == null) {
                            if (c == '\'' || c == '"') {
                                q = c;
                            }
                        }
                    }
                }
            }
            if (b.Length > 0) {
                yield return new CommandToken(b.ToString());
            }
        }
    }
}
