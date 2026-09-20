using System.Collections.Generic;

namespace Brows.Composition;

/// <summary>
/// Information about the state of the import/export framework.
/// </summary>
public interface IExportContext {
    /// <summary>
    /// Finds the first instance of type <typeparamref name="T"/> available in the context.
    /// </summary>
    /// <typeparam name="T">The type of the instance to be returned.</typeparam>
    /// <returns>An instance of type <typeparamref name="T"/> if one is found, otherwise null.</returns>
    T FindImport<T>();

    /// <summary>
    /// Collects all instances of type <typeparamref name="T"/> available in the context.
    /// </summary>
    /// <typeparam name="T">The type of instances to be returned.</typeparam>
    /// <returns>A collection of instances of type <typeparamref name="T"/>.</returns>
    IReadOnlyList<T> ListImports<T>();
}
