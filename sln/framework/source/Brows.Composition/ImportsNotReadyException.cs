using Brows.Composition;

namespace Brows;

/// <summary>
/// The exception thrown when the <see cref="Imports"/> class is not ready.
/// </summary>
public sealed class ImportsNotReadyException : InvalidOperationException {
    internal IImport Agent { get; }

    internal ImportsNotReadyException(IImport agent, string message = null)
    : base(message: message ?? "The imports are not ready.") {
        Agent = agent;
    }
}
