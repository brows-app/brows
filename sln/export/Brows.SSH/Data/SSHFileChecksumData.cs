using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Data {
    internal abstract class SSHFileChecksumData : SSHEntryData<string> {
        protected string HashName { get; }

        protected SSHFileChecksumData(string hashName) {
            HashName = hashName;
        }

        protected sealed override async Task<string> GetValue(SSHEntry entry, Action<string> progress, CancellationToken token) {
            if (entry == null) return null;
            if (entry.Info?.Kind == SSHEntryKind.File) {
                var client = entry.Provider.ClientReady();
                if (client != null) {
                    return await client.CheckSum(HashName, entry.Info, token);
                }
            }
            return null;
        }

        private sealed class ChecksumMD5 : SSHFileChecksumData {
            public ChecksumMD5() : base("MD5") {
                Width = 225;
            }
        }

        private sealed class ChecksumSHA1 : SSHFileChecksumData {
            public ChecksumSHA1() : base("SHA1") {
                Width = 275;
            }
        }

        private sealed class ChecksumSHA256 : SSHFileChecksumData {
            public ChecksumSHA256() : base("SHA256") {
                Width = 450;
            }
        }

        private sealed class ChecksumSHA512 : SSHFileChecksumData {
            public ChecksumSHA512() : base("SHA512") {
                Width = 900;
            }
        }
    }
}
