using System.Net;

namespace Brows.IPC;

/// <summary>
/// Represents the state of a host involved in an object call, including its network endpoint.
/// </summary>
/// <remarks>
/// This class encapsulates information about a host's network endpoint, such as its IP address and port
/// number. It is immutable and provides access to the endpoint details for use in network-related operations.
/// </remarks>
public sealed class ObjectCallHostState {
    /// <summary>
    /// Gets the port number associated with the end point.
    /// </summary>
    public int Port => EndPoint.Port;

    /// <summary>
    /// Gets the network end point associated with the current connection.
    /// </summary>
    public IPEndPoint EndPoint { get; }

    /// <summary>
    /// Represents the state of a host involved in an object call, including its network endpoint.
    /// </summary>
    /// <param name="endPoint">The network endpoint associated with the host. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="endPoint"/> is null.
    /// </exception>
    public ObjectCallHostState(IPEndPoint endPoint) {
        EndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
    }
}
