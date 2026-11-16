using Domore.Logs;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Brows.Gui;

public abstract class CollectionController<TController> : Controller<TController> where TController : class {
    private static readonly ILog Log = Logging.For(typeof(CollectionController<TController>));

    private void Items_CurrentChanged(object sender, EventArgs e) {
        if (Log.Info()) {
            Log.Info(
                nameof(Items_CurrentChanged),
                nameof(Items.CurrentItem) + " > " + Items.CurrentItem,
                nameof(Items.CurrentPosition) + " > " + Items.CurrentPosition);
        }
        CurrentChanged?.Invoke(this, e);
    }

    protected object CurrentItem => Items?.CurrentItem;
    protected ItemCollection Items => ItemsControl?.Items;

    protected virtual ItemsControl ItemsControl { get; }

    protected CollectionController(FrameworkElement element) : base(element) {
    }

    protected CollectionController(FrameworkElement element, ItemsControl itemsControl) : this(element) {
        ItemsControl = itemsControl;
        ItemsControlReady();
    }

    protected void ItemsControlReady() {
        Items?.CurrentChanged += Items_CurrentChanged;
    }

    public event EventHandler CurrentChanged;

    public int CurrentPosition => Items.CurrentPosition;
}

public abstract class CollectionController<TController, TItem> : CollectionController<TController>
where TController : class where TItem : class {
    new public TItem CurrentItem => base.CurrentItem as TItem;

    public CollectionController(FrameworkElement element) : base(element) {
    }

    public CollectionController(FrameworkElement element, ItemsControl itemsControl) : base(element, itemsControl) {
    }
}
