using System;
using System.Collections.Generic;

namespace Brows.MBox.Format;

/// <summary>
/// Collects the problems found while reading a mailbox, within a bounded number of entries.
/// </summary>
/// <remarks>
/// The last slot of the collection is reserved for a summary of the problems that were not
/// retained, so a caller always learns that the report was shortened.
/// </remarks>
internal sealed class MBoxDiagnosticSink {
    /// <summary>
    /// The maximum number of diagnostics that are retained.
    /// </summary>
    internal const int Limit = 128;

    private readonly Action<MBoxDiagnostic> _Callback;
    private readonly List<MBoxDiagnostic> _Items = [];
    private int _Omitted;

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxDiagnosticSink"/> class.
    /// </summary>
    /// <param name="callback">
    /// The callback that receives the problems as they are found. May be <see langword="null"/>.
    /// </param>
    internal MBoxDiagnosticSink(Action<MBoxDiagnostic> callback) {
        _Callback = callback;
    }

    /// <summary>
    /// Gets the problems that were retained.
    /// </summary>
    internal IReadOnlyList<MBoxDiagnostic> Items =>
        _Items;

    /// <summary>
    /// Records a problem.
    /// </summary>
    /// <param name="diagnostic">
    /// The problem to record.
    /// </param>
    internal void Add(MBoxDiagnostic diagnostic) {
        if (diagnostic is null) {
            return;
        }
        if (_Items.Count >= Limit - 1) {
            _Omitted++;
            return;
        }
        _Items.Add(diagnostic);
        _Callback?.Invoke(diagnostic);
    }

    /// <summary>
    /// Records the problems of a collection.
    /// </summary>
    /// <param name="diagnostics">
    /// The problems to record.
    /// </param>
    internal void AddRange(IEnumerable<MBoxDiagnostic> diagnostics) {
        if (diagnostics is null) {
            return;
        }
        foreach (var diagnostic in diagnostics) {
            Add(diagnostic);
        }
    }

    /// <summary>
    /// Reports the problems that were not retained.
    /// </summary>
    internal void Summarize() {
        if (_Omitted == 0) {
            return;
        }
        var summary = new MBoxDiagnostic(
            MBoxDiagnosticCode.DiagnosticSummary,
            0,
            $"{_Omitted} problem(s) were not retained because the diagnostic limit was reached.");
        _Items.Add(summary);
        _Callback?.Invoke(summary);
    }
}
