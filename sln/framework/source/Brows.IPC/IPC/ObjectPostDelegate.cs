using Brows.Composition;
using Domore.Logs;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Brows.IPC;

/// <summary>
/// Base class for types that implement <see cref="IObjectPostDelegate"/>.
/// </summary>
public abstract class ObjectPostDelegate : IObjectPostDelegate {
    private static readonly ILog Log = Logging.For(typeof(ObjectPostDelegate));

    private readonly Lock SubscribeLocker = new();
    private readonly Channel<object> Ch = Channel.CreateUnbounded<object>();

    private Task<bool> SubscribeTask;

    /// <summary>
    /// When overridden in a derived class, starts the event-post system for the delegate.
    /// </summary>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>
    /// A task that results in <see langword="true"/> if events will be posted, 
    /// or <see langword="false"/> if no events will be posted.
    /// </returns>
    protected abstract Task<bool> Subscribe(CancellationToken token);

    /// <summary>
    /// Posts an event.
    /// </summary>
    /// <param name="obj">The object of the event.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>A task that completes when the event has been posted.</returns>
    protected Task Post(object obj, CancellationToken token) {
        return Ch.Writer.WriteAsync(obj, token).AsTask();
    }

    /// <summary>
    /// Completes the event-post system, meaning no further events will be posted.
    /// </summary>
    /// <param name="error">
    /// The error that caused the completion, if one exists.
    /// </param>
    protected void Complete(Exception error = null) {
        Ch.Writer.Complete(error);
    }

    /// <summary>
    /// When overridden in a derived class, gets the type resolver used by the delegate.
    /// </summary>
    public abstract IObjectTypeResolver TypeResolver { get; }

    async IAsyncEnumerable<object> IObjectPostDelegate.Posts([EnumeratorCancellation] CancellationToken token) {
        if (SubscribeTask == null) {
            lock (SubscribeLocker) {
                if (SubscribeTask == null) {
                    var subscribeTask = Subscribe(token);
                    Thread.MemoryBarrier(); // Required for double-checked locking
                    SubscribeTask = subscribeTask;
                }
            }
        }
        var subscribed = await SubscribeTask;
        if (subscribed == false) {
            if (Log.Warn()) {
                Log.Warn("Not subscribed, nothing to post");
            }
            yield break;
        }
        var reader = Ch.Reader;
        for (; ; ) {
            /*
             * WaitToReadAsync returns false when the channel writer has
             * been completed (via the Complete method), meaning no more
             * items will ever be available. This is the normal exit
             * condition for the event stream.
             */
            var read = await reader.WaitToReadAsync(token);
            if (read == false) {
                break;
            }
            if (reader.TryRead(out var item)) {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Implementation of <see cref="IObjectPostFactory"/> that creates instances of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IObjectPostDelegate"/> to create.</typeparam>
    protected internal abstract class Factory<T> : IObjectPostFactory where T : IObjectPostDelegate {
        [ImportRequired]
        internal IImportAgent Imports { get; set; }

        string IObjectPostFactory.Name => typeof(T).Name;

        IObjectPostDelegate IObjectPostFactory.Create() {
            var imports = Imports;
            if (imports is null) {
                throw new InvalidOperationException("The import agent is null.");
            }
            return imports.Construct<T>();
        }
    }
}
