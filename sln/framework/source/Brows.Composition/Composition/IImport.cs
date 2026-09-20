using System;

namespace Brows.Composition;

/// <summary>
/// Imports implementations of <see cref="IExport"/> for the extension framework. Each implementation 
/// is created once and that instance is a long-living object.
/// </summary>
/// <remarks>
/// If an implementation supports the contract <see cref="IExportAndVary"/>, import variables are passed
/// to the instance before the instance is made available.
/// 
/// If an implementation supports the contract <see cref="IExportAndInit"/>, initialization logic is 
/// automatically run before the instance is made available. This initialization occurs after the call
/// to <see cref="IExportAndVary.Vary(IExportVariables, CancellationToken)"/>, if the implementation
/// also supports <see cref="IExportAndVary"/>.
/// 
/// If an implementation supports the contract <see cref="IExportAndKill"/>, clean-up work in the instance's
/// <see cref="IExportAndKill.Kill"/> method will run upon calling <see cref="IImport.Kill"/>.
/// </remarks>
public interface IImport : IImportAgent {
    /// <summary>
    /// Gets the environment in which the imports are used.
    /// </summary>
    IImportEnvironment Environment { get; }

    /// <summary>
    /// De-initializes the import container.
    /// </summary>
    void Kill();

    /// <summary>
    /// Queues an action to be invoked when the imports are ready.
    /// </summary>
    /// <param name="action">The action to be invoked when the imports are ready.</param>
    void ReadyCallback(Action action);
}
