using Brows.Composition;
using Brows.Entries;
using Brows.Gui;
using Brows.Operations;
using Brows.Panels;
using Brows.Sys;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Providers;

/// <summary>
/// Provides a base for panel content, navigation, detail views, and provider lifetime management.
/// </summary>
public abstract class Provider : Notifier,
                                 IProvider,
                                 IControlled<IProviderController>,
                                 IDisposable,
                                 IAsyncDisposable {
    private static readonly ILog Log = Logging.For(typeof(Provider));

    private readonly ConcurrentDictionary<Type, IExport> Imports = [];
    private readonly Lock LifecycleLocker = new();
    private readonly Lock TokenLocker = new();
    private volatile CancellationTokenSource TokenSource = new();
    private IProviderController Controller;

    private Task LifecycleTask;
    private int EndStarted;
    private bool DisposeRequested;
    private bool LifetimeDisposed;

    private event EventHandler ControllerChangedEvent;

    private void SetController(IProviderController value) {
        if (ReferenceEquals(Controller, value)) {
            return;
        }
        Controller = value;
        ControllerChangedEvent?.Invoke(this, EventArgs.Empty);
    }

    private Task Begin(CancellationToken token, bool begun) {
        if (token.IsCancellationRequested) {
            return Task.FromCanceled(token);
        }
        if (begun) {
            Navigation?.Provided(this);
        }
        return Task.CompletedTask;
    }

    private void End(bool ended) {
        if (ended) {
            return;
        }
        Exception error = null;
        try {
            Navigation?.Provided(null);
        }
        catch (Exception ex) {
            error = ex;
        }
        try {
            EndProvided();
        }
        catch (Exception ex) {
            error ??= ex;
        }
        if (error is not null) {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    private protected CancellationTokenSource LinkedTokenSource(CancellationToken other, out CancellationToken token) {
        var t = Token;
        if (t == other) {
            token = t;
            return null;
        }
        if (CancellationToken.None == other) {
            token = t;
            return null;
        }
        var
        source = CancellationTokenSource.CreateLinkedTokenSource(t, other);
        token = source.Token;
        return source;
    }

    private Task EnsureLifetime(bool dispose) {
        TaskCompletionSource completion;
        lock (LifecycleLocker) {
            if (dispose) {
                DisposeRequested = true;
            }

            var current = LifecycleTask;
            var currentCanBeReused = current is not null &&
                                     current.IsFaulted == false &&
                                     current.IsCanceled == false;
            var disposalIsPending = current is not null &&
                                    current.IsCompletedSuccessfully &&
                                    DisposeRequested &&
                                    LifetimeDisposed == false;
            var lifetimeTaskCanBeReused = currentCanBeReused && disposalIsPending == false;
            if (lifetimeTaskCanBeReused) {
                return current;
            }

            completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            LifecycleTask = completion.Task;
        }

        _ = CompleteLifetime(completion);
        _ = completion.Task.ContinueWith(
            continuationAction: completed => {
                var error = completed.Exception?.GetBaseException();
                var errorShouldBeLogged = error is not null && Log.Error();
                if (errorShouldBeLogged) {
                    Log.Error(nameof(DisposeAsync), ID, error);
                }
            },
            cancellationToken: CancellationToken.None,
            continuationOptions: TaskContinuationOptions.OnlyOnFaulted |
                                 TaskContinuationOptions.ExecuteSynchronously,
            scheduler: TaskScheduler.Default);
        return completion.Task;
    }

    private static void RethrowCompletedFailure(Task lifetime) {
        /*
         * Preserve synchronous End/Dispose failures when cleanup completed inline.
         * Pending asynchronous cleanup is retained and observed without blocking.
         */
        if (lifetime.IsFaulted) {
            ExceptionDispatchInfo.Capture(lifetime.Exception.InnerException).Throw();
        }
    }

    private async Task CompleteLifetime(TaskCompletionSource completion) {
        try {
            await Detail.ShutdownAsync();
            Exception error = null;
            try {
                EndProvider();
            }
            catch (Exception ex) {
                error = ex;
            }

            bool shouldDispose;
            lock (LifecycleLocker) {
                shouldDispose = DisposeRequested && LifetimeDisposed == false;
                if (shouldDispose == false) {
                    if (error is null) {
                        completion.TrySetResult();
                    }
                    else {
                        completion.TrySetException(error);
                    }
                    return;
                }
            }
            if (shouldDispose) {
                try {
                    Dispose(true);
                    GC.SuppressFinalize(this);
                    lock (LifecycleLocker) {
                        LifetimeDisposed = true;
                    }
                }
                catch (Exception ex) {
                    error ??= ex;
                }
            }
            if (error is not null) {
                ExceptionDispatchInfo.Capture(error).Throw();
            }
            completion.TrySetResult();
        }
        catch (Exception ex) {
            lock (LifecycleLocker) {
                if (ReferenceEquals(LifecycleTask, completion.Task)) {
                    LifecycleTask = null;
                }
            }
            completion.TrySetException(ex);
        }
    }
    private void EndProvider() {
        if (Interlocked.Exchange(ref EndStarted, 1) != 0) {
            return;
        }
        if (Log.Info()) {
            Log.Info(Log.Join(nameof(End), ID));
        }
        Exception error = null;
        try {
            if (TokenSource != null) {
                lock (TokenLocker) {
                    if (TokenSource != null) {
                        TokenSource.Cancel();
                    }
                }
            }
        }
        catch (Exception ex) {
            error = ex;
        }
        Panel = null;
        Import = null;
        try {
            End(ended: false);
        }
        catch (Exception ex) {
            error ??= ex;
        }
        try {
            End();
        }
        catch (Exception ex) {
            error ??= ex;
        }
        try {
            End(ended: true);
        }
        catch (Exception ex) {
            error ??= ex;
        }
        if (error is not null) {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    TExport IProvider.Import<TExport>() {
        var import = Import;
        if (import is null) {
            return default;
        }
        return (TExport)Imports.GetOrAdd(typeof(TExport), tExport => {
            var type = GetType();
            var exportType = typeof(IProviderExport<>).MakeGenericType(type);
            var importForType = import.List<IExport>().Where(i => exportType.IsAssignableFrom(i.GetType()));
            var exportForType = importForType.OfType<TExport>().FirstOrDefault();
            return exportForType;
        });
    }

    async void IProvider.Begin() {
        if (Log.Info()) {
            Log.Info(Log.Join(nameof(Begin), ID));
        }
        var token = Token;
        try {
            await Begin(token, begun: false);
            await Begin(token);
            await Begin(token, begun: true);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            if (Log.Debug()) {
                Log.Debug(Log.Join(nameof(Begin), nameof(OperationCanceledException), ID));
            }
        }
        catch (Exception ex) {
            if (Log.Error()) {
                Log.Error(nameof(Begin), ID, ex);
            }
        }
    }

    internal IPanel Panel { get; set; }
    internal IImportAgent Import { get; set; }

    internal virtual Task Init(CancellationToken token) {
        using (LinkedTokenSource(token, out var t)) {
            if (t.IsCancellationRequested) {
                return Task.FromCanceled(t);
            }
            var panel = Panel;
            var panelIsNotNull = panel is not null;
            Provider previous = null;
            var panelHasProvider = panelIsNotNull && panel.HasProvider(out previous);
            if (panelHasProvider) {
                var navigation = previous.Navigation;
                if (navigation != null) {
                    Navigation = navigation.For(this);
                }
                Detail.Inherit(previous);
            }
        }
        return Task.CompletedTask;
    }

    internal void DetachController(IProviderController expected) {
        if (ReferenceEquals(Controller, expected)) {
            SetController(null);
        }
    }

    /*
     * A provider is shown by the view attached as its controller, and the
     * provider of entries overrides this to focus its entries instead.
     */
    private protected virtual bool Focus() {
        return Controller?.Focus() ?? false;
    }

    private protected virtual void EndProvided() {
    }

    /// <summary>
    /// Gets a token canceled when the provider ends.
    /// </summary>
    protected internal CancellationToken Token {
        get {
            if (TokenSource != null) {
                lock (TokenLocker) {
                    if (TokenSource != null) {
                        return TokenSource.Token;
                    }
                }
            }
            return new CancellationToken(canceled: true);
        }
    }

    /// <summary>
    /// Initializes a provider.
    /// </summary>
    /// <param name="id">
    /// The ID of the provider.
    /// </param>
    /// <remarks>
    /// A provider that derives directly from <see cref="Provider"/> provides no entries, and is shown
    /// by a view of its own. A view whose template hosts the provider in a <c>ProviderGrid</c>
    /// receives keyboard focus when the panel of the provider is activated. A provider of entries
    /// derives from <see cref="Provider{TEntry, TConfig}"/>.
    /// </remarks>
    protected Provider(string id) {
        ID = id;
    }

    /// <summary>
    /// Releases resources owned by the provider.
    /// </summary>
    /// <param name="disposing">
    /// True for explicit disposal; false when called during finalization.
    /// </param>
    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            if (TokenSource != null) {
                lock (TokenLocker) {
                    var tokenSource = TokenSource;
                    if (tokenSource != null) {
                        TokenSource = null;
                        tokenSource.Dispose();
                    }
                }
            }
        }
    }

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
    protected virtual Task<bool> Take(ISystemMessage message, CancellationToken token) {
        return Task.FromResult(false);
    }

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
    protected virtual Task<bool> Drop(IPanelDrop data, IOperationProgress progress, CancellationToken token) {
        return Task.FromResult(false);
    }

    /// <summary>
    /// Refreshes the provider content.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when the refresh finishes.
    /// </returns>
    protected virtual Task Refresh(CancellationToken token) => Task.CompletedTask;

    /// <summary>
    /// Starts provider-specific work after the provider has been opened.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when startup finishes.
    /// </returns>
    protected virtual Task Begin(CancellationToken token) => Task.CompletedTask;

    /// <summary>
    /// Performs provider-specific cleanup when the provider ends.
    /// </summary>
    protected virtual void End() {
    }

    /// <summary>
    /// Retrieves credentials from the owning panel, asking the user when needed.
    /// </summary>
    /// <typeparam name="TSecret">
    /// The kind of credentials to retrieve.
    /// </typeparam>
    /// <param name="key">
    /// The key used to identify stored credentials.
    /// </param>
    /// <param name="promptFormat">
    /// The format string for the user prompt.
    /// </param>
    /// <param name="promptArgs">
    /// The values to insert into the prompt.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The credentials, or null if none are available or the provider has no panel.
    /// </returns>
    protected async Task<TSecret> GetSecret<TSecret>(string key,
                                                     string promptFormat,
                                                     IEnumerable<string> promptArgs,
                                                     CancellationToken token)
    where TSecret : class, IPanelSecret {
        var panel = Panel;
        if (panel == null) {
            return null;
        }
        return await panel.GetSecret<TSecret>(key, promptFormat, promptArgs, token);
    }

    /// <summary>
    /// Gets the identifier used to open this provider.
    /// </summary>
    public string ID { get; }

    /// <summary>
    /// Gets the parent location identifier, or null when no parent is supplied.
    /// </summary>
    public virtual string Parent { get; }

    /// <summary>
    /// Gets the detail view associated with this provider.
    /// </summary>
    public ProviderDetailSet Detail => field ??=
        new(this);

    /// <summary>
    /// Gets or sets the provider navigation model.
    /// </summary>
    public ProviderNavigation Navigation {
        get;
        set => Change(ref field, value);
    }

    /// <summary>
    /// Opens another provider in the same panel.
    /// </summary>
    /// <param name="id">
    /// The identifier of the provider to open.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the replacement provider was opened; otherwise, false.
    /// </returns>
    public async Task<bool> Change(string id, CancellationToken token) {
        var panel = Panel;
        if (panel == null) return false;
        return await panel.Provide(id, token);
    }

    /// <summary>
    /// Starts closing the detail view, ending the provider, and releasing its resources. Use DisposeAsync to await
    /// completion.
    /// </summary>
    public void Dispose() {
        RethrowCompletedFailure(EnsureLifetime(dispose: true));
    }

    /// <summary>
    /// Asynchronously closes provider details before ending and disposing the provider.
    /// </summary>
    /// <returns>
    /// A task that completes after the provider and its details have been closed and disposed.
    /// </returns>
    public async ValueTask DisposeAsync() {
        await EnsureLifetime(dispose: true);
    }

    /// <summary>
    /// Releases provider resources during finalization.
    /// </summary>
    ~Provider() {
        Dispose(false);
    }

    event EventHandler IProvider.ControllerChanged {
        add => ControllerChangedEvent += value;
        remove => ControllerChangedEvent -= value;
    }

    bool IProvider.Focus() {
        return Focus();
    }

    IProviderController IControlled<IProviderController>.Controller {
        set => SetController(value);
    }

    async Task IProvider.Refresh(CancellationToken token) {
        using (LinkedTokenSource(token, out var t)) {
            var task = Refresh(t);
            if (task != null) {
                await task;
            }
        }
    }

    async Task<bool> IProvider.Take(ISystemMessage message, CancellationToken token) {
        using (LinkedTokenSource(token, out var t)) {
            var task = Take(message, t);
            if (task != null) {
                return await task;
            }
            return false;
        }
    }

    async Task<bool> IProvider.Drop(IPanelDrop data, IOperationProgress progress, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(Log.Join(nameof(Drop), ID));
        }
        using (LinkedTokenSource(token, out var t)) {
            var task = Drop(data, progress, t);
            if (task != null) {
                return await task;
            }
            return false;
        }
    }

    void IProvider.End() {
        RethrowCompletedFailure(EnsureLifetime(dispose: false));
    }
}

/// <summary>
/// Provides a panel's entries using a specified entry type and configuration type.
/// </summary>
/// <typeparam name="TEntry">
/// The type of entry supplied by the provider.
/// </typeparam>
/// <typeparam name="TConfig">
/// The configuration type used by the provider.
/// </typeparam>
public abstract class Provider<TEntry, TConfig> : EntryProvider.Of<TEntry>.With<TConfig>
where TEntry : Entry
where TConfig : EntryConfig, new() {
    /// <summary>
    /// Initializes a provider of entries.
    /// </summary>
    /// <param name="id">
    /// The provider identifier.
    /// </param>
    /// <param name="initialCapacity">
    /// The initial capacity reserved for entries.
    /// </param>
    /// <param name="compareID">
    /// The comparer used for entry identifiers, or null for the default comparer.
    /// </param>
    /// <param name="compareName">
    /// The comparer used for entry names, or null for the default comparer.
    /// </param>
    protected Provider(string id,
                       int initialCapacity,
                       IEqualityComparer<string> compareID,
                       IEqualityComparer<string> compareName)
    : base(id, initialCapacity, compareID, compareName) {
    }

    /// <summary>
    /// Initializes a provider of entries.
    /// </summary>
    /// <param name="id">
    /// The provider identifier.
    /// </param>
    protected Provider(string id) : this(id, 0, null, null) {
    }
}
