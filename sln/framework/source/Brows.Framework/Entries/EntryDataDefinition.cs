using Brows.Commands;
using Brows.Composition;
using Domore.Notification;
using System;
using System.Collections;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Brows.Entries;

/// <summary>
/// Provides column display settings and operations for entry data values.
/// </summary>
public abstract class EntryDataDefinition : Notifier, IEntryDataDefinition {
    private static readonly PropertyChangedEventArgs WidthEvent = new(nameof(Width));
    private static readonly PropertyChangedEventArgs AlignmentEvent = new(nameof(Alignment));
    private static readonly PropertyChangedEventArgs ConverterEvent = new(nameof(Converter));
    private static readonly PropertyChangedEventArgs ConverterParameterEvent = new(nameof(ConverterParameter));

    private protected EntryDataDefinition(string key = null) {
        Key = key ?? GetType().Name;
    }

    internal static EntryDataDefinition Empty { get; } = EntryDataDefinition<Entry, object>.Default;

    /// <summary>
    /// Gets the entry type supported by this definition.
    /// </summary>
    public abstract Type EntryType { get; }

    /// <summary>
    /// Gets the resource key builder for this data definition.
    /// </summary>
    public abstract IExportResourceKey ResourceKey { get; }

    /// <summary>
    /// Gets or sets the preferred column width. NaN lets the view choose the width.
    /// </summary>
    public double Width {
        get;
        set => Change(ref field, value, WidthEvent);
    } = double.NaN;

    /// <summary>
    /// Gets or sets the horizontal alignment used to display values.
    /// </summary>
    public EntryDataAlignment Alignment {
        get;
        set => Change(ref field, value, AlignmentEvent);
    } = EntryDataAlignment.Default;

    /// <summary>
    /// Gets or sets the converter used to format values, or null to use the value directly.
    /// </summary>
    public IEntryDataConverter Converter {
        get;
        set => Change(ref field, value, ConverterEvent);
    }

    /// <summary>
    /// Gets or sets the optional setting passed to the display converter.
    /// </summary>
    public object ConverterParameter {
        get;
        set => Change(ref field, value, ConverterParameterEvent);
    }

    /// <summary>
    /// Gets the optional display name for this data definition.
    /// </summary>
    public virtual string Name => null;

    /// <summary>
    /// Gets the optional display group for this data definition.
    /// </summary>
    public virtual string Group => null;

    /// <summary>
    /// Gets the key identifying this data definition.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Loads this data value for an entry.
    /// </summary>
    /// <param name="entry">
    /// The entry whose value is requested.
    /// </param>
    /// <param name="progress">
    /// Receives intermediate values while loading.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The loaded value.
    /// </returns>
    public abstract Task<object> GetValue(IEntry entry, Action<object> progress, CancellationToken token);

    /// <summary>
    /// Compares this definition's values for two entries.
    /// </summary>
    /// <param name="x">
    /// The first entry.
    /// </param>
    /// <param name="y">
    /// The second entry.
    /// </param>
    /// <returns>
    /// A negative value if x precedes y, zero if equal, or a positive value if x follows y.
    /// </returns>
    public abstract int CompareValue(IEntry x, IEntry y);

    /// <summary>
    /// Refreshes any data needed to load an entry value again.
    /// </summary>
    /// <param name="entry">
    /// The entry whose value is being refreshed.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when refresh work finishes.
    /// </returns>
    public virtual Task RefreshValue(IEntry entry, CancellationToken token) {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines whether to suggest this data key for the current command context.
    /// </summary>
    /// <param name="context">
    /// The command context requesting key suggestions.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the key may be suggested; otherwise, false.
    /// </returns>
    public virtual Task<bool> SuggestKey(ICommandContext context, CancellationToken token) {
        return Task.FromResult(true);
    }
}

/// <summary>
/// Defines a typed data value for a particular entry type.
/// </summary>
/// <typeparam name="TEntry">
/// The entry type whose data is read.
/// </typeparam>
/// <typeparam name="TValue">
/// The type of value read for each entry.
/// </typeparam>
public abstract class EntryDataDefinition<TEntry, TValue> : EntryDataDefinition where TEntry : IEntry {
    private sealed class ComponentResourceKey : IExportResourceKey {
        public string For(string key) {
            return $"{typeof(TEntry).Name}_{key}";
        }
    }

    private sealed class DefaultDefinition : EntryDataDefinition<TEntry, TValue> {
        private static readonly Task<TValue> ValueTask = Task.FromResult(default(TValue));

        protected sealed override Task<TValue> GetValue(TEntry entry,
                                                        Action<TValue> progress,
                                                        CancellationToken cancellationToken) {
            return ValueTask;
        }
    }

    internal static EntryDataDefinition<TEntry, TValue> Default { get; } = new DefaultDefinition();

    /// <summary>
    /// Initializes a typed entry data definition.
    /// </summary>
    /// <param name="key">
    /// The data key, or null to use the concrete type name.
    /// </param>
    protected EntryDataDefinition(string key = null) : base(key) {
    }

    /// <summary>
    /// Loads a typed data value for an entry.
    /// </summary>
    /// <param name="entry">
    /// The entry whose value is requested.
    /// </param>
    /// <param name="progress">
    /// Receives intermediate values while loading.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The loaded value.
    /// </returns>
    protected abstract Task<TValue> GetValue(TEntry entry,
                                             Action<TValue> progress,
                                             CancellationToken cancellationToken);

    /// <summary>
    /// Refreshes any data needed to load a typed entry value again.
    /// </summary>
    /// <param name="entry">
    /// The entry whose value is being refreshed.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when refresh work finishes.
    /// </returns>
    protected virtual Task RefreshValue(TEntry entry, CancellationToken token) {
        return token.IsCancellationRequested
            ? Task.FromCanceled(token)
            : Task.CompletedTask;
    }

    /// <summary>
    /// Gets the entry type supported by this definition.
    /// </summary>
    public sealed override Type EntryType => typeof(TEntry);

    /// <summary>
    /// Gets the resource key builder associated with the entry type.
    /// </summary>
    public sealed override IExportResourceKey ResourceKey => _ResourceKey ??= new ComponentResourceKey();
    private IExportResourceKey _ResourceKey;

    /// <summary>
    /// Loads a value when the entry has the supported type.
    /// </summary>
    /// <param name="entry">
    /// The entry whose value is requested.
    /// </param>
    /// <param name="progress">
    /// Receives intermediate values while loading.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The loaded value, or null when the entry type is not supported.
    /// </returns>
    public sealed override async Task<object> GetValue(IEntry entry,
                                              Action<object> progress,
                                              CancellationToken cancellationToken) {
        if (entry is TEntry tentry) {
            return await GetValue(tentry, value => progress?.Invoke(value), cancellationToken);
        }
        return null;
    }

    /// <summary>
    /// Compares the currently loaded values for two entries.
    /// </summary>
    /// <param name="x">
    /// The first entry.
    /// </param>
    /// <param name="y">
    /// The second entry.
    /// </param>
    /// <returns>
    /// A negative value if x precedes y, zero if equal, or a positive value if x follows y.
    /// </returns>
    public sealed override int CompareValue(IEntry x, IEntry y) {
        return Comparer.Default.Compare(x?[Key]?.Value, y?[Key]?.Value);
    }

    /// <summary>
    /// Refreshes data for a supported entry; unsupported entry types require no action.
    /// </summary>
    /// <param name="entry">
    /// The entry whose value is being refreshed.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when refresh work finishes.
    /// </returns>
    public sealed override Task RefreshValue(IEntry entry, CancellationToken token) {
        return
            token.IsCancellationRequested ? Task.FromCanceled(token) :
            entry is TEntry e ? RefreshValue(e, token) :
            Task.CompletedTask;
    }
}
