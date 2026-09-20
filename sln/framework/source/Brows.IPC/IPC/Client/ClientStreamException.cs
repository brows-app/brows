namespace Brows.IPC.Client;

/// <summary>
/// The exception that wraps all other exceptions thrown when
/// attempting to create or use client streams.
/// </summary>
/// <remarks>
/// Instances of this type should be the only exceptions thrown
/// when creation or use of a client stream fails. That way,
/// catching exceptions of this type is an easy way to know
/// when communication fails.
/// </remarks>
internal sealed class ClientStreamException : Exception {
    /// <summary>
    /// Gets the instance of <see cref="ClientStreamProvider"/> that
    /// provided the stream.
    /// </summary>
    public ClientStreamProvider Provider { get; }

    /// <summary>
    /// Creates a new instance of the exception.
    /// </summary>
    /// <param name="provider">The instance of <see cref="ClientStreamProvider"/> that provided the stream.</param>
    /// <param name="innerException">
    /// The actual exception that was thrown during creation or use of the stream.
    /// This parameter should not be null.
    /// </param>
    public ClientStreamException(ClientStreamProvider provider, Exception innerException) : base(
        message: $"An error occurred while connecting to '{provider}'.",
        innerException: innerException) {
        Provider = provider;
    }
}
