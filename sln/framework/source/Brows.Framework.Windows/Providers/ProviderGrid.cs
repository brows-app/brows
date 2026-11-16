using Brows.Gui;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Brows.Providers;

[TemplatePart(Name = ContentPartName, Type = typeof(FrameworkElement))]
public sealed class ProviderGrid : Control, IProviderController {
    static ProviderGrid() {
        FocusableProperty.OverrideMetadata(typeof(ProviderGrid), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(ProviderGrid), new FrameworkPropertyMetadata(false));
    }

    private const string ContentPartName = "PART_Content";

    private FrameworkElement ContentPart;
    private Provider AttachedProvider;

    private void AttachProvider() {
        var next = IsLoaded ? Provider : null;
        if (ReferenceEquals(AttachedProvider, next)) {
            return;
        }
        DetachProvider();
        AttachedProvider = next;
        if (next is not null) {
            ((IControlled<IProviderController>)next).Controller = this;
        }
    }

    private void DetachProvider() {
        var provider = AttachedProvider;
        AttachedProvider = null;
        provider?.DetachController(this);
    }

    private void This_Loaded(object sender, RoutedEventArgs e) {
        AttachProvider();
    }

    private void This_Unloaded(object sender, RoutedEventArgs e) {
        DetachProvider();
    }

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(object), typeof(ProviderGrid));

    public static readonly DependencyProperty IconTemplateProperty = DependencyProperty.Register(
        nameof(IconTemplate), typeof(DataTemplate), typeof(ProviderGrid));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(object), typeof(ProviderGrid));

    public static readonly DependencyProperty TextTemplateProperty = DependencyProperty.Register(
        nameof(TextTemplate), typeof(DataTemplate), typeof(ProviderGrid));

    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(object), typeof(ProviderGrid));

    public static readonly DependencyProperty ContentTemplateProperty = DependencyProperty.Register(
        nameof(ContentTemplate), typeof(DataTemplate), typeof(ProviderGrid));

    public static readonly DependencyProperty ProviderProperty = DependencyProperty.Register(
        name: nameof(Provider),
        propertyType: typeof(Provider),
        ownerType: typeof(ProviderGrid),
        typeMetadata: new PropertyMetadata(
            defaultValue: null,
            propertyChangedCallback: (sender, args) => ((ProviderGrid)sender).AttachProvider()));

    public object Icon {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public DataTemplate IconTemplate {
        get => GetValue(IconTemplateProperty) as DataTemplate;
        set => SetValue(IconTemplateProperty, value);
    }

    public object Text {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public DataTemplate TextTemplate {
        get => GetValue(TextTemplateProperty) as DataTemplate;
        set => SetValue(TextTemplateProperty, value);
    }

    public object Content {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public DataTemplate ContentTemplate {
        get => GetValue(ContentTemplateProperty) as DataTemplate;
        set => SetValue(ContentTemplateProperty, value);
    }

    public Provider Provider {
        get => GetValue(ProviderProperty) as Provider;
        set => SetValue(ProviderProperty, value);
    }

    public ProviderGrid() {
        Loaded += This_Loaded;
        Unloaded += This_Unloaded;
    }

    public override void OnApplyTemplate() {
        base.OnApplyTemplate();
        ContentPart = GetTemplateChild(ContentPartName) as FrameworkElement;
    }

    /*
     * The view of a provider without entries is focused by moving focus to the
     * first element of the content that can take it.
     */
    bool IProviderController.Focus() {
        var content = ContentPart;
        if (content is null) {
            return false;
        }
        if (content.IsKeyboardFocusWithin) {
            return true;
        }
        return content.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }
}
