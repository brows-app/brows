using System.Collections.Generic;

namespace Brows.Commands;

/// <summary>
/// Provides a base for browsable data associated with a command.
/// </summary>
public abstract class CommandContextData : ICommandContextData {
    /// <summary>
    /// Gets the current data item.
    /// </summary>
    public abstract object Current { get; }

    /// <summary>
    /// Gets configuration text associated with the current item, or null when none is supplied.
    /// </summary>
    public virtual string Conf { get; }

    /// <summary>
    /// Gets command input associated with the current item, or null when none is supplied.
    /// </summary>
    public virtual string Input { get; }

    /// <summary>
    /// Gets the palette input behavior associated with the current item.
    /// </summary>
    public virtual ICommandContextFlag Flag { get; protected set; }

    /// <summary>
    /// Gets the command associated with this data.
    /// </summary>
    public ICommand Command { get; }

    /// <summary>
    /// Initializes browsable data for a command.
    /// </summary>
    /// <param name="command">
    /// The command associated with the data.
    /// </param>
    public CommandContextData(ICommand command) {
        Command = command;
    }

    /// <summary>
    /// Removes the current data item. The default implementation leaves the context unchanged.
    /// </summary>
    /// <returns>
    /// This context unless overridden.
    /// </returns>
    public virtual ICommandContextData Remove() {
        return this;
    }

    /// <summary>
    /// Clears the command data. The default implementation leaves the context unchanged.
    /// </summary>
    /// <returns>
    /// This context unless overridden.
    /// </returns>
    public virtual ICommandContextData Clear() {
        return this;
    }

    /// <summary>
    /// Moves to the next data item. The default implementation leaves the context unchanged.
    /// </summary>
    /// <returns>
    /// This context unless overridden.
    /// </returns>
    public virtual ICommandContextData Next() {
        return this;
    }

    /// <summary>
    /// Moves to the previous data item. The default implementation leaves the context unchanged.
    /// </summary>
    /// <returns>
    /// This context unless overridden.
    /// </returns>
    public virtual ICommandContextData Previous() {
        return this;
    }

    /// <summary>
    /// Accepts the current data item. The default implementation leaves the context unchanged.
    /// </summary>
    /// <returns>
    /// This context unless overridden.
    /// </returns>
    public virtual ICommandContextData Enter() {
        return this;
    }
}

/// <summary>
/// Provides navigation and removal for a current item in a command's data list.
/// </summary>
/// <typeparam name="T">
/// The type of item in the command data list.
/// </typeparam>
public abstract class CommandContextData<T> : CommandContextData {
    /// <summary>
    /// Creates a data context for a position in the supplied list.
    /// </summary>
    /// <param name="index">
    /// The zero-based position of the current item.
    /// </param>
    /// <param name="list">
    /// The data list to use.
    /// </param>
    /// <returns>
    /// The new data context.
    /// </returns>
    protected abstract CommandContextData<T> Create(int index, IList<T> list);

    private bool ItemLoaded;
    private T _Item;

    private bool SameItem(T x, T y) {
        return typeof(T).IsValueType
            ? EqualityComparer<T>.Default.Equals(x, y)
            : ReferenceEquals(x, y);
    }

    private int FindItemIndex(T item) {
        for (var i = 0; i < List.Count; i++) {
            if (SameItem(List[i], item)) {
                return i;
            }
        }
        return -1;
    }

    private int CurrentIndex {
        get {
            var item = Item;
            if (ItemLoaded == false) {
                return -1;
            }
            var index = Index;
            if (index >= 0 && index < List.Count && SameItem(List[index], item)) {
                return index;
            }
            return FindItemIndex(item);
        }
    }

    /// <summary>
    /// Initializes a data context and captures its current item when the index is valid.
    /// </summary>
    /// <param name="command">
    /// The command associated with the data.
    /// </param>
    /// <param name="index">
    /// The initial zero-based item position.
    /// </param>
    /// <param name="list">
    /// The data list to use.
    /// </param>
    protected CommandContextData(ICommand command, int index, IList<T> list) : base(command) {
        Index = index;
        List = list ?? throw new ArgumentNullException(nameof(list));
        CaptureItem();
    }

    private void CaptureItem() {
        var index = Index;
        if (index >= 0 && index < List.Count) {
            _Item = List[index];
            ItemLoaded = true;
        }
    }

    /// <summary>
    /// Handles removal of the current data item.
    /// </summary>
    /// <param name="item">
    /// The removed item.
    /// </param>
    protected virtual void Removed(T item) {
    }

    /// <summary>
    /// Handles clearing of the data list.
    /// </summary>
    /// <param name="items">
    /// The items that were removed.
    /// </param>
    protected virtual void Cleared(IEnumerable<T> items) {
    }

    /// <summary>
    /// Gets the captured current item.
    /// </summary>
    public override object Current =>
        Item;

    /// <summary>
    /// Gets the zero-based item position supplied when this context was created.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the list containing the command data.
    /// </summary>
    public IList<T> List { get; }

    /// <summary>
    /// Gets the captured current item, or the default value when no item has been captured.
    /// </summary>
    public T Item {
        get {
            if (ItemLoaded == false) {
                CaptureItem();
            }
            return _Item;
        }
    }

    /// <summary>
    /// Removes the captured item and chooses a remaining item at or before its position.
    /// </summary>
    /// <returns>
    /// A context for the remaining item, or null if the item is missing or the list is empty.
    /// </returns>
    public override ICommandContextData Remove() {
        var i = CurrentIndex;
        if (i < 0) {
            return null;
        }
        var item = Item;
        List.RemoveAt(i);
        Removed(item);
        while (List.Count <= i) {
            i--;
        }
        if (i >= 0) {
            return Create(i, List);
        }
        return null;
    }

    /// <summary>
    /// Removes all data items and reports the removed items to <see cref="Cleared"/>.
    /// </summary>
    /// <returns>
    /// Null, because no current item remains.
    /// </returns>
    public override ICommandContextData Clear() {
        var items = new List<T>(List);
        List.Clear();
        Cleared(items);
        return null;
    }

    /// <summary>
    /// Moves to the next data item, wrapping from the last item to the first.
    /// </summary>
    /// <returns>
    /// The next context; null if the captured item is missing. An empty list creates a context at index zero.
    /// </returns>
    public override ICommandContextData Next() {
        if (List.Count == 0) {
            return Create(0, List);
        }
        var index = CurrentIndex;
        if (index < 0) {
            return null;
        }
        var next = index + 1;
        if (List.Count > next) {
            return Create(next, List);
        }
        return Create(0, List);
    }

    /// <summary>
    /// Moves to the previous data item, wrapping from the first item to the last.
    /// </summary>
    /// <returns>
    /// The previous context, or null if the list is empty or the captured item is missing.
    /// </returns>
    public override ICommandContextData Previous() {
        if (List.Count == 0) {
            return null;
        }
        var index = CurrentIndex;
        if (index < 0) {
            return null;
        }
        var prev = index - 1;
        if (prev >= 0) {
            return Create(prev, List);
        }
        return Create(List.Count - 1, List);
    }
}
