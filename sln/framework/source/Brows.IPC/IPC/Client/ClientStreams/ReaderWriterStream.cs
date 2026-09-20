using Domore.Logs;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IPC.Client.ClientStreams;

/// <summary>
/// This implementation of <see cref="ClientStream"/> uses instances of <see cref="Stream"/> 
/// to do reading and writing.
/// </summary>
internal abstract class ReaderWriterStream : ClientStream {
    private static readonly ILog Log = Logging.For(typeof(SingleStream));

    private static readonly Encoding DefaultEncoding = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly Lock ReadLocker = new();
    private readonly Lock WriteLocker = new();

    private StreamReader Reader;
    private StreamWriter Writer;
    private Task<Stream> ReadStreamTask;
    private Task<Stream> WriteStreamTask;
    private Stream ReadStream;
    private Stream WriteStream;

    protected Encoding Encoding { get; }

    protected ReaderWriterStream(Encoding encoding = null) {
        Encoding = encoding ?? DefaultEncoding;
    }

    protected abstract Task<Stream> GetReadStream(CancellationToken token);
    protected abstract Task<Stream> GetWriteStream(CancellationToken token);

    protected override void Dispose(bool disposing) {
        if (disposing) {
            try {
                Reader?.Dispose();
            }
            catch (Exception ex) {
                /*
                 * Don't throw in calls to Dispose().
                 */
                Log.Warn(ex);
            }
            try {
                Writer?.Dispose();
            }
            catch (Exception ex) {
                /*
                 * Don't throw in calls to Dispose().
                 */
                Log.Warn(ex);
            }
        }
        base.Dispose(disposing);
    }

    public sealed override async Task<string> Read(CancellationToken token) {
        if (ReadStreamTask is null) {
            lock (ReadLocker) {
                if (ReadStreamTask is null) {
                    /*
                     * Get the stream used for reading.
                     * This is done only once during the lifetime of the
                     * instance. Subsequent calls use the cached value.
                     */
                    var readStreamTask = GetReadStream(token);
                    Thread.MemoryBarrier(); // Required for double-checked locking
                    ReadStreamTask = readStreamTask;
                }
            }
        }
        ReadStream = await ReadStreamTask;

        if (Reader is null) {
            lock (ReadLocker) {
                if (Reader is null) {
                    /*
                     * Construct the reader only once during the lifetime
                     * of the instance. Subsequent calls use the cached value.
                     * 
                     * The parameter 'leaveOpen' is true because we don't know
                     * the lifetime of the actual stream. Descendant implementations
                     * and/or consumers of the implementation are responsible for
                     * closing the actual stream.
                     */
                    var reader = new StreamReader(
                        stream: ReadStream,
                        encoding: Encoding,
                        detectEncodingFromByteOrderMarks: false,
                        bufferSize: 1024,
                        leaveOpen: true);
                    Thread.MemoryBarrier(); // Required for double-checked locking
                    Reader = reader;
                }
            }
        }
        return await Reader.ReadLineAsync(
#if !NETFRAMEWORK
            token
#endif
        ).ConfigureAwait(false);
    }

    public sealed override async Task Write(string value, CancellationToken token) {
        if (WriteStreamTask is null) {
            lock (WriteLocker) {
                if (WriteStreamTask is null) {
                    /*
                     * Get the stream used for writing.
                     * This is done only once during the lifetime of the
                     * instance. Subsequent calls use the cached value.
                     */
                    var writeStreamTask = GetWriteStream(token);
                    Thread.MemoryBarrier(); // Required for double-checked locking
                    WriteStreamTask = writeStreamTask;
                }
            }
        }
        WriteStream = await WriteStreamTask;

        if (Writer is null) {
            lock (WriteLocker) {
                if (Writer is null) {
                    /*
                     * Construct the writer only once during the lifetime
                     * of the instance. Subsequent calls use the cached value.
                     * 
                     * The parameter 'leaveOpen' is true because we don't know
                     * the lifetime of the actual stream. Descendant implementations
                     * and/or consumers of the implementation are responsible for
                     * closing the actual stream.
                     * 
                     * The property 'AutoFlush' is false because we know when
                     * we want to flush: immediately after every message is written.
                     * 
                     * 'NewLine' is explicitly set to avoid potential carriage-return
                     * characters in the messages.
                     */
                    var writer = new StreamWriter(
                        stream: WriteStream,
                        encoding: Encoding,
                        bufferSize: 1024,
                        leaveOpen: true) {
                        AutoFlush = false,
                        NewLine = "\n"
                    };
                    Thread.MemoryBarrier(); // Required for double-checked locking
                    Writer = writer;
                }
            }
        }
        await Writer.WriteLineAsync(value).ConfigureAwait(false);
        await Writer.FlushAsync(
#if !NETFRAMEWORK
            token
#endif
        ).ConfigureAwait(false);
    }
}
