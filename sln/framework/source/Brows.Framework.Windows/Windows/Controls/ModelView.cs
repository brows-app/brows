using Brows.Gui;
using System.Windows;
using System.Windows.Controls;

namespace Brows.Windows.Controls;

public abstract class ModelView : Control {
    static ModelView() {
        FocusableProperty.OverrideMetadata(typeof(ModelView), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(ModelView), new FrameworkPropertyMetadata(false));
    }
}

public abstract class ModelView<TViewModel> : ModelView {
    protected virtual void OnViewModelChanged(TViewModel oldValue, TViewModel newValue) {
    }

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        name: nameof(ViewModel),
        propertyType: typeof(TViewModel),
        ownerType: typeof(ModelView<TViewModel>),
        typeMetadata: new PropertyMetadata(
            defaultValue: default(TViewModel),
            propertyChangedCallback: (s, e) => {
                if (s is ModelView<TViewModel> self) {
                    self.OnViewModelChanged(
                        oldValue: e.OldValue is TViewModel oldValue ? oldValue : default,
                        newValue: e.NewValue is TViewModel newValue ? newValue : default);
                }
            }));

    public TViewModel ViewModel {
        get => (TViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
}

public abstract class ModelView<TViewModel, TController> : ModelView<TViewModel>
where TController : class {
    private void This_Loaded(object sender, RoutedEventArgs e) {
        if (ViewModel is IControlled<TController> controlled) {
            controlled.Controller = this as TController;
        }
    }

    private void This_Unloaded(object sender, RoutedEventArgs e) {
        if (ViewModel is IControlled<TController> controlled) {
            controlled.Controller = null;
        }
    }

    protected ModelView() {
        Loaded += This_Loaded;
        Unloaded += This_Unloaded;
    }

    protected override void OnViewModelChanged(TViewModel oldValue, TViewModel newValue) {
        if (oldValue is IControlled<TController> oldControlled) {
            oldControlled.Controller = null;
        }
        var isLoaded = IsLoaded;
        if (isLoaded) {
            if (newValue is IControlled<TController> newControlled) {
                newControlled.Controller = this as TController;
            }
        }
        base.OnViewModelChanged(oldValue, newValue);
    }
}
