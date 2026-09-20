using Brows.IPC.Client.ClientStreams;
using Domore.Logs;
using System.IO;
using System.Threading.Tasks;

namespace Brows.IPC.Client.ClientStreamProviders;

/// <summary>
/// This implementation of <see cref="ClientStreamProvider"/> simply
/// wraps an existing instance of <see cref="Stream"/>.
/// </summary>
internal sealed class ExistingStreamProvider : ClientStreamProvider {
    private static readonly ILog Log = Logging.For(typeof(ExistingStreamProvider));

    protected sealed override void Dispose(bool disposing) {
        if (disposing) {
            /*
             * Check the flag, so that we don't dispose anything 
             * unless we were told to dispose it.
             */
            if (DisposeStream) {
                try {
                    ExistingStream.Dispose();
                }
                catch (Exception ex) {
                    /*
                     * Hopefully we never get here, but we definitely
                     * don't want to throw exceptions from Dispose.
                     */
                    Log.Warn(ex);
                }
            }
        }
        base.Dispose(disposing);
    }

    protected sealed override Task<ClientStream> GetStream(CancellationToken token) {
        return token.IsCancellationRequested
            ? Task.FromCanceled<ClientStream>(token)
            : Task.FromResult<ClientStream>(new SingleStream(ExistingStream));
    }

    /// <summary>
    /// Gets a flag that indicates whether or not the stream wrapped by the provider
    /// is disposed when the provider itself is disposed. If true, the stream is
    /// disposed when the provider is disposed. If false, the stream is not disposed.
    /// </summary>
    public bool DisposeStream { get; }

    /// <summary>
    /// Gets the stream that is wrapped by the provider.
    /// </summary>
    public Stream ExistingStream { get; }

    /// <summary>
    /// Creates a new wrapper for an existing stream.
    /// </summary>
    /// <param name="existingStream">The stream that is wrapped.</param>
    /// <param name="disposeStream">
    /// Whether or not to call <see cref="Stream.Dispose()"/> on <paramref name="existingStream"/>
    /// when the instance of <see cref="ExistingStreamProvider"/> is disposed. If true, the existing
    /// stream will be disposed when the wrapper is disposed. If false, the existing stream will not
    /// be disposed.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="existingStream"/> is null.
    /// </exception>
    public ExistingStreamProvider(Stream existingStream, bool disposeStream) {
        DisposeStream = disposeStream;
        ExistingStream = existingStream ?? throw new ArgumentNullException(nameof(existingStream));
    }

    public sealed override string ToString() {
        return ExistingStream.ToString();
    }
}
