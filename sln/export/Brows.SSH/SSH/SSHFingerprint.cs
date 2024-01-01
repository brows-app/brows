using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.SSH {
    public sealed class SSHFingerprint {
        public string HexString =>
            _HexString ?? (
            _HexString = BitConverter.ToString(_Hash).Replace("-", "").ToLowerInvariant());
        private string _HexString;

        public string Base64String =>
            _Base64String ?? (
            _Base64String = Convert.ToBase64String(_Hash));
        private string _Base64String;

        public IReadOnlyList<byte> Hash => _Hash;
        private readonly byte[] _Hash;

        public string HashFunc => _HashFunc;
        private readonly string _HashFunc;

        public SSHFingerprint(string hashFunc, IEnumerable<byte> hash) {
            if (null == hash) throw new ArgumentNullException(nameof(hash));
            _Hash = hash.ToArray();
            _HashFunc = hashFunc;
        }

        public sealed override string ToString() {
            return $"{HashFunc}:{Base64String.TrimEnd('=')}";
        }
    }
}
