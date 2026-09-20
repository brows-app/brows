using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Implementations of this interface also implement <see cref="IExport"/>
/// and are able to initialize themselves before being used in the framework.
/// Initialization occurs soon after instantiation at the start of the program.
/// </summary>
public interface IExportAndInit : IExport {
    /// <summary>
    /// Called to initialize the instance at the start of the program, before
    /// it is available to importers.
    /// </summary>
    /// <param name="context">Information about the state of the framework at the time of initialization.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>A task that, upon completion, signals that initialization of the instance is complete.</returns>
    Task Init(IExportContext context, CancellationToken token);
}
