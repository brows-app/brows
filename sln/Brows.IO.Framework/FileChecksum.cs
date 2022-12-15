using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using IO;

    internal class FileChecksum : EntryData<string> {
        protected override async Task<string> Access(CancellationToken cancellationToken) {
            var file = File;
            if (file != null) {
                switch (Key) {
                    case nameof(FileInfoExtension.ChecksumMD5):
                        return await file.ChecksumMD5(cancellationToken);
                    case nameof(FileInfoExtension.ChecksumSHA1):
                        return await file.ChecksumSHA1(cancellationToken);
                    case nameof(FileInfoExtension.ChecksumSHA256):
                        return await file.ChecksumSHA256(cancellationToken);
                    case nameof(FileInfoExtension.ChecksumSHA512):
                        return await file.ChecksumSHA512(cancellationToken);
                }
            }
            return null;
        }

        public FileInfo File { get; }

        public FileChecksum(string key, FileInfo file, CancellationToken cancellationToken) : base(key, cancellationToken) {
            File = file;
        }
    }
}
