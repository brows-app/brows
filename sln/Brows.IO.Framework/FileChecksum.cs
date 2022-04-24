using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using IO;

    internal class FileChecksum : EntryData {
        protected override async Task<object> Access() {
            var file = File;
            if (file != null) {
                switch (Key) {
                    case nameof(FileInfoExtension.ChecksumMD5):
                        return await file.ChecksumMD5(CancellationToken);
                    case nameof(FileInfoExtension.ChecksumSHA1):
                        return await file.ChecksumSHA1(CancellationToken);
                    case nameof(FileInfoExtension.ChecksumSHA256):
                        return await file.ChecksumSHA256(CancellationToken);
                    case nameof(FileInfoExtension.ChecksumSHA512):
                        return await file.ChecksumSHA512(CancellationToken);
                }
            }
            return null;
        }

        public FileInfo File { get; }
        public CancellationToken CancellationToken { get; }

        public FileChecksum(string key, FileInfo file, CancellationToken cancellationToken) : base(key) {
            File = file;
            CancellationToken = cancellationToken;
        }
    }
}
