using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class FtpStreamSource : FileProtocolStreamSource<FtpEntry> {
        protected sealed override async Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            var client = Entry.Client();
            Stream = await client.Read(Entry.Info.Size, token).ConfigureAwait(false);
            return await base.StreamReady(token).ConfigureAwait(false);
        }

        public sealed override string RelativePath => Entry.RelativePath;
        public sealed override long StreamLength => Entry.Info.Size ?? 0;

        public FtpStreamSource(FtpEntry entry) : base(entry) {
        }
    }
}
