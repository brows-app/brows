using System.Threading.Tasks;

namespace Brows.Programs;

/// <summary>
/// Defines a named program that can run with a startup context.
/// </summary>
public interface IProgram {
    /// <summary>
    /// Gets the program's name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Runs the program with the supplied startup context.
    /// </summary>
    /// <param name="context">
    /// The startup arguments and configuration source.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The program exit code.
    /// </returns>
    Task<int> Run(IProgramContext context, CancellationToken cancellationToken);
}
