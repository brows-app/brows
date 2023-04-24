using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Data {
    internal class FileChecksumData : FileSystemInfoData<string> {
        protected string HashName { get; }

        protected FileChecksumData(string hashName) {
            HashName = hashName;
        }

        protected sealed override async Task<string> GetValue(FileSystemEntry entry, Action<string> progress, CancellationToken cancellationToken) {
            if (entry == null) return null;
            if (entry.Info is FileInfo file) {
                var hash = default(byte[]);
                var stream = await Task.Run(file.OpenRead, cancellationToken);
                await using (stream) {
                    using (var hashAlgorithm = HashAlgorithm.Create(HashName)) {
                        hash = await hashAlgorithm.ComputeHashAsync(stream, cancellationToken);
                    }
                }
                return BitConverter
                    .ToString(hash)
                    .Replace("-", "")
                    .ToLowerInvariant();
            }
            return null;
        }

        private class ChecksumMD5 : FileChecksumData {
            public ChecksumMD5() : base("MD5") {
                Width = 250;
            }
        }

        private class ChecksumSHA1 : FileChecksumData {
            public ChecksumSHA1() : base("SHA1") {
                Width = 250;
            }
        }

        private class ChecksumSHA256 : FileChecksumData {
            public ChecksumSHA256() : base("SHA256") {
                Width = 250;
            }
        }

        private class ChecksumSHA512 : FileChecksumData {
            public ChecksumSHA512() : base("SHA512") {
                Width = 500;
            }
        }
    }
}
