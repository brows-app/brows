using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Url.Ftp {
    internal sealed class FtpListingToken {
        private Dictionary<string, FtpListingToken> Chunks;

        private long? GetBase16() {
            const long max = (long.MaxValue - 15) / 16;
            long num = 0;
            for (var i = 0; i < View.Length; ++i) {
                if (num > max) {
                    return null;
                }
                var c = View[i];
                if (c >= '0' && c <= '9') {
                    num *= 16;
                    num += c - '0';
                }
                else if (c >= 'a' && c <= 'f') {
                    num *= 16;
                    num += c - '0' + 10;
                }
                else if (c >= 'A' && c <= 'F') {
                    num *= 16;
                    num += c - 'A' + 10;
                }
                else {
                    return null;
                }
            }
            return num;
        }

        private long? GetBase10OnLeft() {
            const long max = (long.MaxValue - 9) / 10;
            long num = 0;
            for (var i = 0; i < View.Length; ++i) {
                if (View[i] < '0' || View[i] > '9') {
                    break;
                }
                if (num > max) {
                    return null;
                }
                num *= 10;
                num += View[i] - '0';
            }
            return num;
        }

        private long? GetBase10OnRight() {
            const long max = (long.MaxValue - 9) / 10;
            long? num = null;
            var start = View.Length - 1;
            while (View[start - 1] >= '0' && View[start - 1] <= '9') {
                --start;
            }
            for (var i = start; i < View.Length; ++i) {
                if (num > max) {
                    num = null;
                    break;
                }
                num ??= 0;
                num *= 10;
                num += View[i] - '0';
            }
            return num;
        }

        public char this[int n] => View[n];

        public bool IsBase10 => _IsBase10 ??= View.All(c => '0' <= c && c <= '9');
        private bool? _IsBase10;

        public bool IsBase10OnLeft => _IsBase10OnLeft ??= !(View.Length < 2 || View[0] < '0' || View[0] > '9');
        private bool? _IsBase10OnLeft;

        public bool IsBase10OnRight => _IsBase10OnRight ??= !(View.Length < 2 || View[^1] < '0' || View[^1] > '9');
        private bool? _IsBase10OnRight;

        public bool IsBase16 => _IsBase16 ??= View.All(c => '0' <= c && c <= '9' || 'A' <= c && c <= 'F' || 'a' <= c && c <= 'f');
        private bool? _IsBase16;

        public long? Base10 => _Base10 ??=
            IsBase10 || IsBase10OnLeft ? GetBase10OnLeft() :
            IsBase10OnRight ? GetBase10OnRight() :
            null;
        private long? _Base10;

        public long? Base16 => _Base16 ??= IsBase16 ? GetBase16() : null;
        private long? _Base16;

        public string Data { get; }
        public string View { get; }
        public int Start { get; }
        public int Length { get; }

        public FtpListingToken(string data, int start, int length) {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            View = data.Substring(start, length);
            Start = start;
            Length = length;
        }

        public FtpListingToken(string data) {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            View = Data;
            Start = 0;
            Length = View.Length;
        }

        public FtpListingToken Chunk(int start, int length) {
            if (length < 0) {
                length = View.Length - start;
            }
            var key = $"{start}..{length}";
            var chunks = Chunks ??= [];
            if (chunks.TryGetValue(key, out var chunk) == false) {
                chunks[key] = chunk = new FtpListingToken(View, start, length);
            }
            return chunk;
        }

        public int Find(string any, int start = 0) {
            if (null == any) {
                return -1;
            }
            for (var i = start; i < View.Length; ++i) {
                for (var c = 0; c < any.Length; ++c) {
                    if (View[i] == any[c]) {
                        return i;
                    }
                }
            }
            return -1;
        }

        public int Find(char c, int start = 0) {
            for (var i = start; i < View.Length; ++i) {
                if (View[i] == c) {
                    return i;
                }
            }
            return -1;
        }

        public sealed override string ToString() {
            return View;
        }
    }
}
