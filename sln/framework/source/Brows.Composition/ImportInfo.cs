using Brows.Composition;
using Brows.Composition.ImportCollections;
using System;
using System.Collections.Generic;

namespace Brows;

/// <summary>
/// The base class for information about import composition.
/// </summary>
public abstract class ImportInfo {
    private protected ImportInfo() {
        /*
         * We need to recognize all implementations of this type, 
         * so keep all implementations internal to this assembly.
         */
    }

    internal abstract ImportCollectionFactory CollectionFactory();

    /// <summary>
    /// Gets or sets variables made available to implementations of
    /// <see cref="IExportAndVary"/>.
    /// </summary>
    public ImportVariables Variables { get; init; }

    /// <summary>
    /// Creates information for initializing imports via composition.
    /// </summary>
    /// <param name="variables">The import variables.</param>
    /// <param name="path">The lookup that defines paths to search during composition.</param>
    /// <param name="inject">An optional list of additional imports to inject during composition.</param>
    /// <returns>
    /// The information for initializing imports via composition.
    /// </returns>
    public static ImportInfo Composed(Func<ImportVariables> variables = null,
                                      Func<Dictionary<string, IEnumerable<string>>> path = null,
                                      Func<IEnumerable<IExport>> inject = null) {
        return ComposedImportInfo.Create(variables, path, inject);
    }

    /// <summary>
    /// Creates information for initializing imports via a pre-constructed list.
    /// </summary>
    /// <param name="variables">The import variables.</param>
    /// <param name="list">The list of imports.</param>
    /// <returns>
    /// The information for initializing imports from the list.
    /// </returns>
    public static ImportInfo Listed(Func<ImportVariables> variables = null,
                                    Func<IEnumerable<IExport>> list = null) {
        return ListedImportInfo.Create(variables, list);
    }
}
