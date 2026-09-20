using Brows.IPC.TxRx.ProxyProviders;
using System;
using System.Collections.Generic;
using System.Net;

namespace Brows.IPC.TxRx;

/// <summary>
/// Base class for types that provide TxRx proxies.
/// </summary>
public abstract class TxRxProxyProvider : IDisposable {
    /// <summary>
    /// When overridden in a derived class, releases managed and/or unmanaged resources.
    /// </summary>
    /// <param name="disposing">
    /// True to release both managed and unmanaged resources.
    /// False to only release unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing) {
    }

    /// <summary>
    /// Raises the <see cref="ProxiesChanged"/> event.
    /// </summary>
    /// <param name="e">The arguments for the event.</param>
    protected virtual void OnProxiesChanged(EventArgs e) {
        ProxiesChanged?.Invoke(this, e);
    }

    /// <summary>
    /// The event that is raised when the provided proxies change.
    /// </summary>
    public event EventHandler ProxiesChanged;

    /// <summary>
    /// When overridden in a derived class, starts tracking proxies
    /// and potentially raising <see cref="ProxiesChanged"/>.
    /// </summary>
    public abstract void Start();

    /// <summary>
    /// When overridden in a derived class, gets the current proxies
    /// of the provider.
    /// </summary>
    /// <returns>The current proxies.</returns>
    public abstract IEnumerable<TxRxProxy> GetProxies();

    /// <summary>
    /// Creates a provider based on the given <paramref name="endPoints"/>.
    /// </summary>
    /// <param name="typeResolver">The instance of <see cref="IObjectTypeResolver"/> to use.</param>
    /// <param name="endPoints">The end points from which proxies are created.</param>
    /// <returns>The created instance of <see cref="TxRxProxyProvider"/>.</returns>
    public static TxRxProxyProvider From(IObjectTypeResolver typeResolver, IEnumerable<IPEndPoint> endPoints) {
        return new EndPointProxyProvider(typeResolver, endPoints);
    }

    /// <summary>
    /// Releases managed and unmanaged resources used by the instance.
    /// </summary>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~TxRxProxyProvider() {
        Dispose(false);
    }
}
