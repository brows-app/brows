using System.Threading.Tasks;

namespace Brows.IPC;

/// <summary>
/// Provides a contract for delegates that can process input and produce output.
/// </summary>
public interface IObjectCallDelegate {
    /// <summary>
    /// Gets the type resolver used by the delegate.
    /// </summary>
    IObjectTypeResolver TypeResolver { get; }

    /// <summary>
    /// Processes input and produces output.
    /// </summary>
    /// <param name="input">The input argument.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>A task that results in the output of the process.</returns>
    Task<object> Call(object input, CancellationToken token);
}
