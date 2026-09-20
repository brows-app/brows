using Brows.Composition;
using System.Collections.Generic;
using System.Linq;

namespace Brows;

/// <summary>
/// This is an implementation of <see cref="IExportContext"/> that takes a
/// list of currently available imports as a parameter.
/// </summary>
internal sealed class ImportContext : IExportContext {
    internal object SyncRoot { get; }
    internal IReadOnlyList<IExport> Imports { get; }

    internal ImportContext(IReadOnlyList<IExport> imports, object syncRoot) {
        Imports = imports ?? throw new ArgumentNullException(nameof(imports));
        SyncRoot = syncRoot;
    }

    T IExportContext.FindImport<T>() {
        lock (SyncRoot) {
            return Imports.OfType<T>().FirstOrDefault();
        }
    }

    IReadOnlyList<T> IExportContext.ListImports<T>() {
        lock (SyncRoot) {
            return Imports.OfType<T>().ToList();
        }
    }
}
