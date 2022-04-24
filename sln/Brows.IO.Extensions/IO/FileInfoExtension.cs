using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Threading.Tasks;

    public static class FileInfoExtension {
        public static async Task<bool> ExistsAsync(this FileInfo fileInfo, CancellationToken cancellationToken) {
            if (fileInfo == null) throw new ArgumentNullException(nameof(fileInfo));
            return await Async.Run(cancellationToken, () => fileInfo.Exists);
        }

        public static async Task<FileInfo> TryNewAsync(string path, CancellationToken cancellationToken) {
            return await Async.Run(cancellationToken, () => {
                try {
                    return new FileInfo(path);
                }
                catch {
                    return null;
                }
            });
        }

        public static async Task<string> Checksum(this FileInfo fileInfo, string hashName, CancellationToken cancellationToken) {
            if (fileInfo == null) throw new ArgumentNullException(nameof(fileInfo));

            var exists = await fileInfo.ExistsAsync(cancellationToken);
            if (exists == false) return null;

            var hash = default(byte[]);
            using (var hashAlgorithm = HashAlgorithm.Create(hashName)) {
                var stream = await Async.Run(cancellationToken, () => fileInfo.OpenRead());
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
