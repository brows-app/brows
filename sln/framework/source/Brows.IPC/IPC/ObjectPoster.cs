using Brows.IPC.TxRx;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IPC;

/// <summary>
/// Posts objects produced by an <see cref="IObjectPostDelegate"/> to each
/// TxRx proxy provided by a <see cref="TxRxProxyProvider"/>.
/// </summary>
public sealed class ObjectPoster {
    private readonly Lazy<Task> LazyTask;

    private async Task TaskFactory() {
        var txrxProxies = new TxRxProxyCollection(TxRxProxyProvider, PostDelegate.TypeResolver);
        using (txrxProxies) {
            var posts = PostDelegate.Posts(Token).ConfigureAwait(ContinueOnCapturedContext);
            /*
             * Enumerate events produced by the delegate and fan each one
             * out to every connected TxRx proxy. Each proxy transmits the
             * object and reads a response (the "TxRx" pattern).
             */
            await foreach (var post in posts) {
                await txrxProxies.TxRx(post, TxRxInfo, Token).ConfigureAwait(ContinueOnCapturedContext);
            }
        }
    }

    /// <summary>
    /// Gets the task that completes when every object produced by
    /// <see cref="PostDelegate"/> has been posted. The task is started
    /// the first time this property is accessed.
    /// </summary>
    public Task Task => field ??= LazyTask.Value;

    /// <summary>
    /// Gets a value indicating whether continuations are marshaled back to the captured context.
    /// </summary>
    public bool ContinueOnCapturedContext { get; }

    /// <summary>
    /// Gets the cancellation token for the posting task.
    /// </summary>
    public CancellationToken Token { get; }

    /// <summary>
    /// Gets the delegate that produces the objects to post.
    /// </summary>
    public IObjectPostDelegate PostDelegate { get; }

    /// <summary>
    /// Gets the provider of the TxRx proxies that receive the posted objects.
    /// </summary>
    public TxRxProxyProvider TxRxProxyProvider { get; }

    /// <summary>
    /// Gets the callbacks invoked during each transmit/receive operation.
    /// </summary>
    public TxRxInfo TxRxInfo { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectPoster"/> class.
    /// </summary>
    /// <param name="postDelegate">The delegate that produces the objects to post.</param>
    /// <param name="txrxProxyProvider">The provider of the TxRx proxies that receive the posted objects.</param>
    /// <param name="txrxInfo">The callbacks invoked during each transmit/receive operation.</param>
    /// <param name="continueOnCapturedContext">Whether continuations are marshaled back to the captured context.</param>
    /// <param name="token">The cancellation token for the posting task.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="postDelegate"/> or <paramref name="txrxProxyProvider"/> is <c>null</c>.
    /// </exception>
    public ObjectPoster(IObjectPostDelegate postDelegate,
                        TxRxProxyProvider txrxProxyProvider,
                        TxRxInfo txrxInfo,
                        bool continueOnCapturedContext,
                        CancellationToken token) {
        PostDelegate = postDelegate ?? throw new ArgumentNullException(nameof(postDelegate));
        TxRxProxyProvider = txrxProxyProvider ?? throw new ArgumentNullException(nameof(txrxProxyProvider));
        TxRxInfo = txrxInfo;
        ContinueOnCapturedContext = continueOnCapturedContext;
        Token = token;
        LazyTask = new(TaskFactory, LazyThreadSafetyMode.ExecutionAndPublication);
    }
}
