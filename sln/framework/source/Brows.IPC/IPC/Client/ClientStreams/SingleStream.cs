using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Brows.IPC.Client.ClientStreams;

/// <summary>
/// This implementation uses a single stream for both
/// reading and writing.
/// </summary>
internal sealed class SingleStream : ReaderWriterStream {
    protected sealed override void Dispose(bool disposing) {
        base.Dispose(disposing);
    }

    protected sealed override Task<Stream> GetReadStream(CancellationToken token) {
        return token.IsCancellationRequested
            ? Task.FromCanceled<Stream>(token)
            : Task.FromResult(Stream);
    }

    protected sealed override Task<Stream> GetWriteStream(CancellationToken token) {
        return token.IsCancellationRequested
            ? Task.FromCanceled<Stream>(token)
            : Task.FromResult(Stream);
    }

    public new Encoding Encoding => base.Encoding;

    public Stream Stream { get; }

    public SingleStream(Stream stream, Encoding encoding = null) : base(encoding) {
        Stream = stream;
    }
}
