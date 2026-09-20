using System;
using System.Net;
using System.Threading.Tasks;

namespace Brows.IPC;

/// <summary>
/// Information for an object-call host process.
/// </summary>
/// <remarks>
/// This class is designed to facilitate communication and lifecycle management for object calls. It
/// provides configurable options for TCP keep-alive, network endpoint settings, and various asynchronous callbacks for
/// handling start, finish, and data read/write operations.
/// </remarks>
public sealed class ObjectCallHostRun {
    /// <summary>
    /// Gets or sets a value indicating whether the socket should use the TCP keep-alive option.
    /// </summary>
    /// <remarks>
    /// Enabling the TCP keep-alive option helps detect broken connections by periodically sending 
    /// keep-alive packets. This can be useful in scenarios where long-lived connections are expected.
    /// </remarks>
    public bool SocketKeepAlive { get; set; } = true;

    /// <summary>
    /// Gets or sets the network endpoint used for communication.
    /// </summary>
    public IPEndPoint EndPoint { get; set; }

    /// <summary>
    /// Gets or sets the delegate to be invoked when the object-call host starts.
    /// </summary>
    /// <remarks>
    /// The delegate takes an <see cref="ObjectCallHostState"/> instance and a <see cref="CancellationToken"/>
    /// as parameters and returns a <see cref="Task"/>. It is typically used to perform
    /// initialization or setup logic when the host starts.
    /// </remarks>
    public Func<ObjectCallHostState, CancellationToken, Task> OnStart { get; set; }

    /// <summary>
    /// Gets or sets the callback function to be invoked when the operation finishes.
    /// </summary>
    /// <remarks>
    /// The callback function receives the current <see cref="ObjectCallHostState"/> and 
    /// a <see cref="CancellationToken"/> as parameters. It is expected to return a 
    /// <see cref="Task"/> that represents the asynchronous operation.
    /// </remarks>
    public Func<ObjectCallHostState, CancellationToken, Task> OnFinish { get; set; }

    /// <summary>
    /// Gets or sets a callback function that is invoked when data is read.
    /// </summary>
    /// <remarks>
    /// The callback function takes a string representing the data read and a <see cref="CancellationToken"/> 
    /// to handle cancellation. It returns a <see cref="Task"/>  that represents the
    /// asynchronous operation.
    /// </remarks>
    public Func<string, CancellationToken, Task> OnDataRead { get; set; }

    /// <summary>
    /// Gets or sets the callback function that is invoked when data is written.
    /// </summary>
    /// <remarks>
    /// The callback function takes a string representing the data to be written and a <see cref="CancellationToken"/> 
    /// to support cancellation of the operation. The function  returns a <see cref="Task"/>
    /// that represents the asynchronous write operation.
    /// </remarks>
    public Func<string, CancellationToken, Task> OnDataWrite { get; set; }
}
