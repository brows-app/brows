using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Threading.Tasks;

    public static class FileChecksum {
        public static async Task<string> Checksum(this FileInfo fileInfo, string hashName, CancellationToken cancellationToken) {
            if (fileInfo == null) throw new ArgumentNullException(nameof(fileInfo));
            var hash = default(byte[]);
            using (var hashAlgorithm = HashAlgorithm.Create(hashName)) {
                var stream = await Async.With(cancellationToken).Run(() => fileInfo.OpenRead());
                await using (stream) {
                    hash = await hashAlgorithm.ComputeHashAsync(stream, cancellationToken);
                }
            }
            return BitConverter
                .ToString(hash)
                .Replace("-", "")
                .ToLowerInvariant();
        }

        public static Task<string> ChecksumMD5(this FileInfo fileInfo, CancellationToken cancellationToken) => Checksum(fileInfo, "MD5", cancellationToken);
        public static Task<string> ChecksumSHA1(this FileInfo fileInfo, CancellationToken cancellationToken) => Checksum(fileInfo, "SHA1", cancellationToken);
        public static Task<string> ChecksumSHA256(this FileInfo fileInfo, CancellationToken cancellationToken) => Checksum(fileInfo, "SHA256", cancellationToken);
        public static Task<string> ChecksumSHA512(this FileInfo fileInfo, CancellationToken cancellationToken) => Checksum(fileInfo, "SHA512", cancellationToken);
    }
}
