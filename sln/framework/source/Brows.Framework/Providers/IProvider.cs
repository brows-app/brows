using Brows.Operations;
using Brows.Panels;
using Brows.Sys;
using System;
using System.Threading.Tasks;

namespace Brows.Providers;

/// <summary>
/// Represents a location or content that can be opened in a panel.
/// </summary>
public interface IProvider : IDisposable {
    /// <summary>
    /// Occurs when the view that shows the provider is attached or detached.
    /// </summary>
    event EventHandler ControllerChanged;

    /// <summary>
    /// Gets the identifier used to open this provider.
    /// </summary>
    string ID { get; }

    /// <summary>
    /// Gets the parent location identifier, or null when no parent is supplied.
    /// </summary>
    string Parent { get; }

    /// <summary>
    /// Starts the provider after it has been opened in a panel.
    /// </summary>
    void Begin();

    /// <summary>
    /// Ends the provider and starts closing its associated detail view.
    /// </summary>
    void End();

    /// <summary>
    /// Refreshes the provider content.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when the refresh finishes.
    /// </returns>
    Task Refresh(CancellationToken token);

    /// <summary>
    /// Offers a system message to the provider.
    /// </summary>
    /// <param name="message">
    /// The message to handle.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the provider handled the message; otherwise, false.
    /// </returns>
    Task<bool> Take(ISystemMessage message, CancellationToken token);

    /// <summary>
    /// Handles files offered to the provider through a drop operation.
    /// </summary>
    /// <param name="data">
    /// The drop target and offered file paths.
    /// </param>
    /// <param name="progress">
    /// Receives progress updates for the operation.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the provider handled the drop; otherwise, false.
    /// </returns>
    Task<bool> Drop(IPanelDrop data, IOperationProgress progress, CancellationToken token);

    /// <summary>
    /// Retrieves a service associated with this provider.
    /// </summary>
    /// <typeparam name="TExport">
    /// The provider service contract to retrieve.
    /// </typeparam>
    /// <returns>
    /// The matching service, or the default value when none is available.
    /// </returns>
    TExport Import<TExport>() where TExport : IProviderExport;

    /// <summary>
    /// Requests keyboard focus for the view that shows the provider.
    /// </summary>
    /// <returns>
    /// True if the view has keyboard focus; otherwise, false.
    /// </returns>
    /// <remarks>
    /// The entries of an <see cref="IEntryProvider"/> are focused. Otherwise, the view that shows the
    /// provider is focused.
    /// </remarks>
    bool Focus();
}
