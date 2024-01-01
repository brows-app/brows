using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Data {
    internal abstract class FileChecksumData : FileSystemInfoData<string> {
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
                    using (var hashAlgorithm = (HashAlgorithm)CryptoConfig.CreateFromName(HashName)) {
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

        private sealed class ChecksumMD5 : FileChecksumData {
            public ChecksumMD5() : base("MD5") {
                Width = 225;
            }
        }

        private sealed class ChecksumSHA1 : FileChecksumData {
            public ChecksumSHA1() : base("SHA1") {
                Width = 275;
            }
        }

        private sealed class ChecksumSHA256 : FileChecksumData {
            public ChecksumSHA256() : base("SHA256") {
                Width = 450;
            }
        }

        private sealed class ChecksumSHA512 : FileChecksumData {
            public ChecksumSHA512() : base("SHA512") {
                Width = 900;
            }
        }
    }
}
