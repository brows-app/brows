using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class SftpStreamSource : FileProtocolStreamSource<SftpEntry> {
    protected sealed override async Task<IEntryStreamReady> StreamReady(CancellationToken token) {
        Stream = await Entry.Client.OpenRead(Entry.RemotePath, token);
        return await base.StreamReady(token);
    }

    public sealed override string RelativePath => Entry.RelativePath;
    public sealed override long StreamLength => (Entry.Info.Flags & 1) != 0 && Entry.Info.Size <= long.MaxValue
        ? (long)Entry.Info.Size
        : -1;

    internal SftpStreamSource(SftpEntry entry) : base(entry) {
    }
}
