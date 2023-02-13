using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using FILECHECKSUM = IO.FileChecksum;

    internal class FileChecksum : EntryData<string> {
        protected override async Task<string> Access(CancellationToken cancellationToken) {
            var file = File;
            if (file != null) {
                switch (Key) {
                    case nameof(FILECHECKSUM.ChecksumMD5):
                        return await FILECHECKSUM.ChecksumMD5(file, cancellationToken);
                    case nameof(FILECHECKSUM.ChecksumSHA1):
                        return await FILECHECKSUM.ChecksumSHA1(file, cancellationToken);
                    case nameof(FILECHECKSUM.ChecksumSHA256):
                        return await FILECHECKSUM.ChecksumSHA256(file, cancellationToken);
                    case nameof(FILECHECKSUM.ChecksumSHA512):
                        return await FILECHECKSUM.ChecksumSHA512(file, cancellationToken);
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
