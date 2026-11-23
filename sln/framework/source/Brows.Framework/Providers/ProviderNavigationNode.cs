using Brows.Commands;
using Brows.Gui;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Providers;

/// <summary>
/// Represents a location in a provider's navigation tree.
/// </summary>
public abstract class ProviderNavigationNode : Notifier, IProviderNavigationNode,
                                                         ICommandSourceObject,
                                                         IControlled<IProviderNavigationNodeController> {
    private static readonly ILog Log = Logging.For(typeof(ProviderNavigationNode));

    private bool AddedChildren;
    private bool AddingChildren;
    private readonly ObservableCollection<ProviderNavigationNode> ChildCollection = new();
    private readonly object IconLocker = new();

    private void Controller_Loaded(object sender, EventArgs e) {
        if (AddedChildren) {
            return;
        }
        if (AddingChildren) {
            return;
        }
        AddChildren();
    }

    private void Controller_Unloaded(object sender, EventArgs e) {
        Unloaded();
    }

    private async void AddChildren() {
        var token = Token;
        try {
            AddingChildren = true;
            ChildCollection.Clear();
            await foreach (var child in EnumerateChildren(token)) {
                child.Navigation = Navigation;
                child.Parent = this;
                ChildCollection.Add(child);
                Navigation?.NodeAdded(child);
            }
            AddedChildren = true;
        }
        catch (OperationCanceledException canceled) when (token.IsCancellationRequested) {
            if (Log.Debug()) {
                Log.Debug(Log.Join(nameof(AddChildren), nameof(canceled)));
            }
            ChildCollection.Clear();
        }
        catch {
            ChildCollection.Clear();
            throw;
        }
        finally {
            AddingChildren = false;
        }
    }

    private void ResetIcon() {
        lock (IconLocker) {
            IconGeneration++;
            _Icon = null;
            IconLoaded = false;
            IconLoading = false;
        }
    }

    private async Task LoadIcon(int generation) {
        object icon = null;
        try {
            icon = await GetIcon(Token);
        }
        catch (Exception ex) {
            if (Log.Warn()) {
                Log.Warn(Log.Join(nameof(Icon), nameof(Exception), ID), ex);
            }
        }
        var iconChanged = false;
        lock (IconLocker) {
            if (generation != IconGeneration) {
                return;
            }
            IconLoading = false;
            iconChanged = _Icon != icon;
            _Icon = icon;
            IconLoaded = true;
        }
        if (iconChanged) {
            NotifyPropertyChanged(nameof(Icon));
        }
    }

    private void Collapsed(bool @private) {
        ResetIcon();
        Collapsed();
    }

    private void Expanded(bool @private) {
        ResetIcon();
        if (AddedChildren == false && AddingChildren == false) {
            AddChildren();
        }
        Expanded();
    }

    private async Task<bool> Requested(CancellationToken token) {
        var task = Navigation?.Requested(this, token);
        return task == null
            ? false
            : await task;
    }

    internal ProviderNavigation Navigation {
        get => _Navigation;
        set => Change(ref _Navigation, value, nameof(Navigation));
    }
    private ProviderNavigation _Navigation;

    /// <summary>
    /// Loads the icon to display for this node.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The platform-specific icon, or null when no icon is available.
    /// </returns>
    protected abstract Task<object> GetIcon(CancellationToken token);

    /// <summary>
    /// Loads the child nodes for this location.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The child nodes.
    /// </returns>
    protected abstract IAsyncEnumerable<ProviderNavigationNode> EnumerateChildren(CancellationToken token);

    /// <summary>
    /// Determines whether this node lies on the path to a provider identifier.
    /// </summary>
    /// <param name="id">
    /// The provider identifier to locate.
    /// </param>
    /// <returns>
    /// True if this node should be expanded to reveal that identifier; otherwise, false.
    /// </returns>
    protected internal abstract bool PartOf(string id);

    /// <summary>
    /// Responds to the node being expanded.
    /// </summary>
    protected virtual void Expanded() {
    }

    /// <summary>
    /// Responds to the node being collapsed.
    /// </summary>
    protected virtual void Collapsed() {
    }

    /// <summary>
    /// Determines whether this node represents a provider identifier.
    /// </summary>
    /// <param name="id">
    /// The provider identifier to compare.
    /// </param>
    /// <returns>
    /// True if this node represents the identifier; otherwise, false.
    /// </returns>
    protected internal virtual bool Is(string id) {
        return ID == id;
    }

    /// <summary>
    /// Reloads the node's children.
    /// </summary>
    protected void Refresh() {
        AddChildren();
    }

    /// <summary>
    /// Responds to the node's view being unloaded.
    /// </summary>
    protected virtual void Unloaded() {
    }

    /// <summary>
    /// Gets the child nodes currently loaded for this node.
    /// </summary>
    public IEnumerable<ProviderNavigationNode> Children => ChildCollection;

    /// <summary>
    /// Gets the parent node, or null for the root.
    /// </summary>
    public ProviderNavigationNode Parent {
        get => _Parent;
        private set => Change(ref _Parent, value, nameof(Parent));
    }
    private ProviderNavigationNode _Parent;

    /// <summary>
    /// Gets the provider identifier represented by this node.
    /// </summary>
    public abstract string ID { get; }

    /// <summary>
    /// Gets the name displayed for this node.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets all currently loaded descendant nodes in tree order, excluding this node.
    /// </summary>
    public IEnumerable<ProviderNavigationNode> Descendents {
        get {
            foreach (var child in Children) {
                foreach (var descendent in child.Descendents.Prepend(child)) {
                    yield return descendent;
                }
            }
        }
    }

    /*
     * A load is considered complete even when it yields null or fails, so the
     * getter must test IconLoaded instead of _Icon. Testing _Icon would re-enter
     * the load from every PropertyChanged and recurse without bound whenever the
     * icon is null.
     */
    /// <summary>
    /// Gets the node icon, starting a load when needed. The value may be null until loading finishes.
    /// </summary>
    public object Icon {
        get {
            var startLoad = false;
            var generation = 0;
            lock (IconLocker) {
                if (IconLoaded == false && IconLoading == false) {
                    IconLoading = true;
                    generation = IconGeneration;
                    startLoad = true;
                }
            }
            if (startLoad) {
                _ = LoadIcon(generation);
            }
            lock (IconLocker) {
                return _Icon;
            }
        }
    }
    private object _Icon;
    private bool IconLoaded;
    private bool IconLoading;
    private int IconGeneration;

    /// <summary>
    /// Gets or sets whether the node is selected.
    /// </summary>
    public bool Select {
        get => _Select;
        set => Change(ref _Select, value, nameof(Select));
    }
    private bool _Select;

    /// <summary>
    /// Gets or sets whether the node is expanded, loading children when needed.
    /// </summary>
    public bool Expand {
        get => _Expand;
        set {
            if (Change(ref _Expand, value, nameof(Expand))) {
                if (Expand == true) {
                    Expanded(@private: true);
                }
                else {
                    Collapsed(@private: true);
                }
            }
        }
    }
    private bool _Expand;

    /// <summary>
    /// Gets or sets the token used to cancel icon and child loading.
    /// </summary>
    public CancellationToken Token {
        get => _Token;
        set => Change(ref _Token, value, nameof(Token));
    }
    private CancellationToken _Token;

    object ICommandSourceObject.Instance =>
        this;

    IEnumerable ICommandSourceObject.Collection =>
        new[] { this };

    IProviderNavigationNodeController IControlled<IProviderNavigationNodeController>.Controller {
        set {
            var newValue = value;
            var oldValue = Controller;
            if (oldValue != newValue) {
                if (oldValue != null) {
                    oldValue.Loaded -= Controller_Loaded;
                    oldValue.Unloaded -= Controller_Unloaded;
                }
                if (newValue != null) {
                    newValue.Loaded += Controller_Loaded;
                    newValue.Unloaded += Controller_Unloaded;
                }
                var controller = Controller = newValue;
                if (controller?.IsLoaded == true) {
                    Controller_Loaded(controller, EventArgs.Empty);
                }
            }
        }
    }
    private IProviderNavigationNodeController Controller;
}
