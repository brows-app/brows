using Brows.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.CLI;

/// <summary>
/// A command that may be run in a CLI program.
/// </summary>
public interface ICliCommand : IExport {
    /// <summary>
    /// Gets the name of the command.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets help text for the command.
    /// </summary>
    string Help { get; }

    /// <summary>
    /// Gets the manual for the command.
    /// </summary>
    string Manual { get; }

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="context">The context in which the command is running.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>
    /// An arbitrary object that is the result of the command's process.
    /// </returns>
    Task<object> Run(ICliCommandContext context, CancellationToken token);
}
