using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Url.Ftp {
    internal sealed class FtpListingItem {
        private IReadOnlyList<FtpListingToken> Tokens => _Tokens ??= Split().ToList();
        private IReadOnlyList<FtpListingToken> _Tokens;

        private IEnumerable<FtpListingToken> Split() {
            var start = default(int?);
            for (var i = 0; i < Data.Length; i++) {
                if (Data[i] == ' ' || Data[i] == '\t') {
                    if (start.HasValue) {
                        yield return new FtpListingToken(Data, start.Value, i - start.Value);
                    }
                    start = null;
                }
                else {
                    if (start == null) {
                        start = i;
                    }
                }
            }
            if (start.HasValue) {
                if (start.Value < Data.Length - 1) {
                    yield return new FtpListingToken(Data, start.Value, Data.Length - start.Value);
                }
            }
        }

        public FtpListingToken this[int n] => GetToken(n);

        public string Data { get; }

        public FtpListingItem(string data) {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public FtpListingToken GetToken(int n) {
            return Tokens.Count > n
                ? Tokens[n]
                : null;
        }

        public FtpListingToken GetToken(int n, bool to_end = false, bool include_whitespace = false) {
            if (to_end) {
                return GetEndToken(n, include_whitespace);
            }
            else {
                return GetToken(n);
            }
        }

        public FtpListingToken GetEndToken(int n, bool includeWhitespace = false) {
            var token = GetToken(n);
            if (token == null) {
                return null;
            }
            var start = includeWhitespace
                ? n == 0
                    ? token.Start
                    : GetToken(n - 1).Start + GetToken(n - 1).Length + 1
                : token.Start;
            var length = includeWhitespace
                ? Data.Length - start
                : Data.TrimEnd().Length - start;

            return new FtpListingToken(Data, start, length);
        }

        public sealed override string ToString() {
            return Data;
        }
    }
}
