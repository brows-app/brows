using System.Collections.Generic;

namespace Brows.Commands;

public abstract class CommandContextData : ICommandContextData {
    public abstract object Current { get; }

    public virtual string Conf { get; }
    public virtual string Input { get; }
    public virtual ICommandContextFlag Flag { get; protected set; }
    public ICommand Command { get; }

    public CommandContextData(ICommand command) {
        Command = command;
    }

    public virtual ICommandContextData Remove() {
        return this;
    }

    public virtual ICommandContextData Clear() {
        return this;
    }

    public virtual ICommandContextData Next() {
        return this;
    }

    public virtual ICommandContextData Previous() {
        return this;
    }

    public virtual ICommandContextData Enter() {
        return this;
    }
}

public abstract class CommandContextData<T> : CommandContextData {
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

    protected virtual void Removed(T item) {
    }

    protected virtual void Cleared(IEnumerable<T> items) {
    }

    public override object Current =>
        Item;

    public int Index { get; }
    public IList<T> List { get; }

    public T Item {
        get {
            if (ItemLoaded == false) {
                CaptureItem();
            }
            return _Item;
        }
    }

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

    public override ICommandContextData Clear() {
        var items = new List<T>(List);
        List.Clear();
        Cleared(items);
        return null;
    }

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
