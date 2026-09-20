using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Brows.IPC.Client.ClientStreams;

/// <summary>
/// This implementation uses two streams: one for reading,
/// and one for writing.
/// </summary>
internal sealed class DualStream : ReaderWriterStream {
    protected sealed override void Dispose(bool disposing) {
        base.Dispose(disposing);
    }

    protected sealed override Task<Stream> GetReadStream(CancellationToken token) {
        return token.IsCancellationRequested
            ? Task.FromCanceled<Stream>(token)
            : Task.FromResult(ReadStream);
    }

    protected sealed override Task<Stream> GetWriteStream(CancellationToken token) {
        return token.IsCancellationRequested
            ? Task.FromCanceled<Stream>(token)
            : Task.FromResult(WriteStream);
    }

    public new Encoding Encoding => base.Encoding;

    public Stream ReadStream { get; }
    public Stream WriteStream { get; }

    public DualStream(Stream readStream, Stream writeStream, Encoding encoding = null) : base(encoding) {
        ReadStream = readStream;
        WriteStream = writeStream;
    }
}
