using Brows.Gui;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Brows.Providers;

public sealed class ProviderContent : Control, IProviderDetailSetController {
    static ProviderContent() {
        FocusableProperty.OverrideMetadata(typeof(ProviderContent), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(ProviderContent), new FrameworkPropertyMetadata(false));
    }

    private static readonly DependencyPropertyKey DetailTemplateSelectorKey = DependencyProperty.RegisterReadOnly(
        name: nameof(DetailTemplateSelector),
        propertyType: typeof(DataTemplateSelector),
        ownerType: typeof(ProviderContent),
        typeMetadata: new PropertyMetadata(
            defaultValue: null));

    private ProviderDetailSet AttachedDetails;
    private event EventHandler DetailLoaded;

    private void AttachDetails() {
        var next = IsLoaded ? Provider?.Detail : null;
        if (ReferenceEquals(AttachedDetails, next)) {
            return;
        }
        DetachDetails();
        AttachedDetails = next;
        if (next is not null) {
            ((IControlled<IProviderDetailSetController>)next).Controller = this;
        }
    }

    private void DetachDetails() {
        var details = AttachedDetails;
        AttachedDetails = null;
        details?.DetachController(this);
    }

    private void This_Loaded(object sender, RoutedEventArgs e) {
        AttachDetails();
        DetailLoaded?.Invoke(this, EventArgs.Empty);
    }

    private void This_Unloaded(object sender, RoutedEventArgs e) {
        DetachDetails();
    }

    public static readonly DependencyProperty DetailTemplateSelectorProperty =
        DetailTemplateSelectorKey.DependencyProperty;

    public static readonly DependencyProperty ProviderProperty = DependencyProperty.Register(
        name: nameof(Provider),
        propertyType: typeof(Provider),
        ownerType: typeof(ProviderContent),
        typeMetadata: new PropertyMetadata(
            defaultValue: null,
            propertyChangedCallback: (sender, args) => ((ProviderContent)sender).AttachDetails()));

    public DataTemplateSelector DetailTemplateSelector {
        get => GetValue(DetailTemplateSelectorProperty) as DataTemplateSelector;
        private set => SetValue(DetailTemplateSelectorKey, value);
    }

    public Provider Provider {
        get => GetValue(ProviderProperty) as Provider;
        set => SetValue(ProviderProperty, value);
    }

    public ProviderContent() {
        DetailTemplateSelector = new DetailTemplateSelectorDefault(this);
        Loaded += This_Loaded;
        Unloaded += This_Unloaded;
    }

    event EventHandler IController.Loaded {
        add => DetailLoaded += value;
        remove => DetailLoaded -= value;
    }

    Task IProviderDetailSetController.InvokeAsync(Func<Task> work) {
        ArgumentNullException.ThrowIfNull(work);
        if (Dispatcher.CheckAccess()) {
            return work();
        }
        return Dispatcher.InvokeAsync(work).Task.Unwrap();
    }
    private sealed class DetailTemplateSelectorDefault : DataTemplateSelector {
        public ProviderContent Element { get; }

        public DetailTemplateSelectorDefault(ProviderContent element) {
            Element = element ?? throw new ArgumentNullException(nameof(element));
        }

        public sealed override DataTemplate SelectTemplate(object item, DependencyObject container) {
            if (item == null) {
                return null;
            }
            var key = item.GetType();
            var resource = Element.FindResource(key);
            var template = resource as DataTemplate;
            return template;
        }
    }
}
