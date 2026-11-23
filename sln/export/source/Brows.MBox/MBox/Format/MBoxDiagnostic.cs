using System;

namespace Brows.MBox.Format;

/// <summary>
/// Describes a problem found while reading a mailbox.
/// </summary>
internal sealed record MBoxDiagnostic {
    /// <summary>
    /// Gets the kind of problem that was found.
    /// </summary>
    internal MBoxDiagnosticCode Code { get; }

    /// <summary>
    /// Gets the absolute offset, from the start of the source, of the problem.
    /// </summary>
    internal long Offset { get; }

    /// <summary>
    /// Gets a short description of the problem.
    /// </summary>
    internal string Description { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxDiagnostic"/> class.
    /// </summary>
    /// <param name="code">
    /// The kind of problem that was found.
    /// </param>
    /// <param name="offset">
    /// The absolute offset, from the start of the source, of the problem.
    /// </param>
    /// <param name="description">
    /// A short description of the problem.
    /// </param>
    internal MBoxDiagnostic(MBoxDiagnosticCode code, long offset, string description) {
        Code = code;
        Offset = offset;
        Description = description ?? string.Empty;
    }

    /// <inheritdoc/>
    public override string ToString() {
        return $"[{Code}] @{Offset}: {Description}";
    }
}
