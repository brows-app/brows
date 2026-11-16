using Brows.Operations;
using Brows.Panels;
using Brows.Sys;
using System;
using System.Threading.Tasks;

namespace Brows.Providers;

public interface IProvider : IDisposable {
    /// <summary>
    /// Occurs when the view that shows the provider is attached or detached.
    /// </summary>
    event EventHandler ControllerChanged;

    string ID { get; }
    string Parent { get; }
    void Begin();
    void End();
    Task Refresh(CancellationToken token);
    Task<bool> Take(ISystemMessage message, CancellationToken token);
    Task<bool> Drop(IPanelDrop data, IOperationProgress progress, CancellationToken token);
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
