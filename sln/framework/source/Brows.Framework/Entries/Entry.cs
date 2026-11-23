using Brows.Commands;
using Brows.Panels;
using Brows.Providers;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Brows.Entries;

/// <summary>
/// Provides an entry's identity, selection, data values, and refresh behavior.
/// </summary>
public abstract class Entry : Notifier, IEntry, ICommandSourceObject {
    private static readonly ILog Log = Logging.For(typeof(Entry));
    private static readonly PropertyChangedEventArgs SelectEventArgs = new(nameof(Select));

    private event EntryDropEventHandler DropEvent;
    private event EntrySelectedEventHandler SelectedEvent;
    private event EntryRefreshedEventHandler RefreshedEvent;

    private EntryDataInstance DataInstance => _DataInstance ??= new(this, Provider.Data.Definition, Provider.Token);
    private EntryDataInstance _DataInstance;

    private EntryTaskCollection Task => _Task ??= new();
    private EntryTaskCollection _Task;

    internal void Drop(IPanelDrop drop) {
        DropEvent?.Invoke(this, new EntryDropEventArgs(drop));
    }

    /// <summary>
    /// Gets the token canceled when the owning provider ends.
    /// </summary>
    protected internal CancellationToken Token =>
        Provider.Token;

    /// <summary>
    /// Gets the provider that owns this entry.
    /// </summary>
    protected internal EntryProvider Provider { get; }

    /// <summary>
    /// Initializes an entry owned by a provider.
    /// </summary>
    /// <param name="provider">
    /// The provider that owns the entry.
    /// </param>
    protected Entry(EntryProvider provider) {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>
    /// Responds to a change in this entry's selection state.
    /// </summary>
    /// <param name="select">
    /// Whether the entry is now selected.
    /// </param>
    protected virtual void Selected(bool select) {
    }

    /// <summary>
    /// Performs entry-specific work after its data values have been invalidated.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when the entry-specific refresh finishes.
    /// </returns>
    protected virtual Task Refreshed(CancellationToken token) {
        return token.IsCancellationRequested
            ? System.Threading.Tasks.Task.FromCanceled(token)
            : System.Threading.Tasks.Task.CompletedTask;
    }

    /// <summary>
    /// Gets the entry data value for a key.
    /// </summary>
    /// <param name="key">
    /// The data definition key.
    /// </param>
    /// <returns>
    /// The data associated with the key.
    /// </returns>
    public IEntryData this[string key] =>
        DataInstance[key];

    /// <summary>
    /// Gets the entry identifier within its provider.
    /// </summary>
    public abstract string ID { get; }

    /// <summary>
    /// Gets the display name; by default, the entry identifier.
    /// </summary>
    public virtual string Name => ID;

    /// <summary>
    /// Gets or sets whether the entry is selected, reporting changes to listeners.
    /// </summary>
    public bool Select {
        get => _Select;
        set {
            if (Change(ref _Select, value, SelectEventArgs)) {
                Selected(value);
                SelectedEvent?.Invoke(this, new EntrySelectedEventArgs(value));
            }
        }
    }
    private bool _Select;

    /// <summary>
    /// Refreshes the entry and invalidates its loaded data values.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when the entry refresh finishes.
    /// </returns>
    public async Task Refresh(CancellationToken token) {
        await DataInstance.Refresh(token);
        await Refreshed(token);
        RefreshedEvent?.Invoke(this, new EntryRefreshedEventArgs());
    }

    /// <summary>
    /// Returns the entry identifier.
    /// </summary>
    /// <returns>
    /// The entry identifier.
    /// </returns>
    public override string ToString() {
        return ID;
    }

    event EntryDropEventHandler IEntry.Drop {
        add => DropEvent += value;
        remove => DropEvent -= value;
    }

    event EntrySelectedEventHandler IEntry.Selected {
        add => SelectedEvent += value;
        remove => SelectedEvent -= value;
    }

    event EntryRefreshedEventHandler IEntry.Refreshed {
        add => RefreshedEvent += value;
        remove => RefreshedEvent -= value;
    }

    Task<T> IEntry.Task<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken token) {
        return Task.Get(key, factory, token);
    }

    object ICommandSourceObject.Instance =>
        this;

    IEnumerable ICommandSourceObject.Collection =>
        Provider.Selection;
}

/// <summary>
/// Provides an entry with access to its specific provider type.
/// </summary>
/// <typeparam name="TProvider">
/// The type of provider that owns the entry.
/// </typeparam>
public abstract class Entry<TProvider> : Entry where TProvider : EntryProvider {
    /// <summary>
    /// Gets the provider that owns this entry.
    /// </summary>
    protected new TProvider Provider { get; }

    /// <summary>
    /// Initializes an entry owned by a provider.
    /// </summary>
    /// <param name="provider">
    /// The provider that owns the entry.
    /// </param>
    protected Entry(TProvider provider) : base(provider) {
        Provider = provider;
    }
}
