using Brows.IPC.MessageSubscriptions;
using System;
using System.Collections.Generic;

namespace Brows.IPC;

internal abstract class CommanderMessageSubscription : IDisposable {
    protected virtual void Dispose(bool disposing) {
    }

    public abstract IAsyncEnumerable<CommanderMessage> Messages(CancellationToken token);

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~CommanderMessageSubscription() {
        Dispose(false);
    }

    /// <summary>
    /// Creates a new machine-event subscription to the host.
    /// </summary>
    /// <returns>
    /// The new machine-event subscription.
    /// </returns>
    public static CommanderMessageSubscription ToHost() =>
        new HostSubscription();

    /// <summary>
    /// Creates a new machine-event subscription to a proxy.
    /// </summary>
    /// <returns>
    /// The new machine-event subscription.
    /// </returns>
    public static CommanderMessageSubscription ToProxy(IAsyncEnumerable<CommanderMessage> agent) =>
        new ProxySubscription(agent);
}
