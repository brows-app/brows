using Brows.Security;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public static class SecurityData {
        private static readonly Encoding Encoding = Encoding.UTF8;
        private static ConcurrentDictionary<string, byte[]> Cache = [];

        private static async Task<T> Import<T>(CancellationToken token) {
            var imports = await Imports.Ready(token).ConfigureAwait(false);
            var service = imports.Get<T>() ?? throw new InvalidOperationException($"Missing import: {typeof(T)}");
            return service;
        }

        private static byte[] Encode(string s) {
            return Encoding.GetBytes(s);
        }

        private static string Decode(byte[] b) {
            return Encoding.GetString(b);
        }

        private static async Task<byte[]> Entropy(CancellationToken token) {
            var service = await Import<IGetEntropy>(token);
            var entropy = default(SecureString);
            var worked = await service.Work(set: value => entropy = value, token).ConfigureAwait(false);
            if (worked == false) {
                throw new InvalidOperationException("Entropy did not work.");
            }
            var b = new byte[entropy.Length * 2];
            var p = Marshal.SecureStringToGlobalAllocUnicode(entropy);
            try {
                for (var i = 0; i < b.Length; i++) {
                    b[i] = Marshal.ReadByte(p, i);
                }
            }
            finally {
                Marshal.ZeroFreeGlobalAllocUnicode(p);
            }
            return b;
        }

        public static async Task<byte[]> Protect(byte[] data, CancellationToken token) {
            var service = await Import<IProtectData>(token).ConfigureAwait(false);
            var entropy = await Entropy(token).ConfigureAwait(false);
            var result = default(byte[]);
            var worked = await service.Work(data: data, entropy: entropy, set: data => result = data, token).ConfigureAwait(false);
            if (worked == false) {
                throw new InvalidOperationException("Protect did not work.");
            }
            return result;
        }

        public static async Task<byte[]> Unprotect(byte[] data, CancellationToken token) {
            var service = await Import<IUnprotectData>(token).ConfigureAwait(false);
            var entropy = await Entropy(token).ConfigureAwait(false);
            var result = default(byte[]);
            var worked = await service.Work(data, entropy, set: data => result = data, token).ConfigureAwait(false);
            if (worked == false) {
                throw new InvalidOperationException("Unprotect did not work.");
            }
            return result;
        }

        public static async Task<string> Protect(string data, CancellationToken token) {
            var bytData = await Protect(Encode(data), token).ConfigureAwait(false);
            var strData = Convert.ToBase64String(bytData);
            return strData;
        }

        public static async Task<string> Unprotect(string data, CancellationToken token) {
            var bytData = Convert.FromBase64String(data);
            var newData = await Unprotect(bytData, token).ConfigureAwait(false);
            return Decode(newData);
        }

        public static async Task Save(string key, byte[] data, CancellationToken token) {
            Cache[key] = [.. data];
            var service = await Import<ISaveProtectedData>(token).ConfigureAwait(false);
            var worked = await service.Work(Cache, token).ConfigureAwait(false);
            if (worked == false) {
                throw new InvalidOperationException("Save did not work.");
            }
        }

        private static async Task<byte[]> Load(string key, CancellationToken token) {
            if (Cache == null) {
                var service = await Import<ILoadProtectedData>(token).ConfigureAwait(false);
                var worked = await service.Work(set: value => Cache = new(value), token).ConfigureAwait(false);
                if (worked == false) {
                    throw new InvalidOperationException("Load did not work.");
                }
            }
            return Cache.TryGetValue(key, out var value)
                ? [.. value]
                : null;
        }
    }
}
