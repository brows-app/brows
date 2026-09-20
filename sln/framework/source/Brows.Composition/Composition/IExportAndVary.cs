using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Implementations of this interface may initialize themselves with variables
/// passed to <see cref="ImportVariables.Set{T}(T)"/>.
/// </summary>
public interface IExportAndVary : IExport {
    /// <summary>
    /// Potentially varies the behavior of the instance according to variables.
    /// </summary>
    /// <param name="variables">The available variables.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>
    /// A task that, once complete, indicates that any variation of the instance,
    /// according to the <paramref name="variables"/>, is complete.
    /// </returns>
    Task Vary(IExportVariables variables, CancellationToken token);
}

/// <summary>/
/// Implementations of this interface may initialize themselves with the variable
/// of type <typeparamref name="TVariable"/> passed to <see cref="ImportVariables.Set{T}(T)"/>.
/// </summary>
public interface IExportAndVary<TVariable> : IExport where TVariable : IExportVariable {
    /// <summary>
    /// Potentially varies the behavior of the instance according to variable.
    /// </summary>
    /// <param name="variable">The available variable.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>
    /// A task that, once complete, indicates that any variation of the instance,
    /// according to the <paramref name="variable"/>, is complete.
    /// </returns>
    Task Vary(TVariable variable, CancellationToken token);
}
