using System.Windows;

namespace Brows {
    partial class ProviderGrid {
        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(object), typeof(ProviderGrid));
        public static readonly DependencyProperty IconTemplateProperty = DependencyProperty.Register(nameof(IconTemplate), typeof(DataTemplate), typeof(ProviderGrid));

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(object), typeof(ProviderGrid));
        public static readonly DependencyProperty TextTemplateProperty = DependencyProperty.Register(nameof(TextTemplate), typeof(DataTemplate), typeof(ProviderGrid));

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object), typeof(ProviderGrid));
        public static readonly DependencyProperty ContentTemplateProperty = DependencyProperty.Register(nameof(ContentTemplate), typeof(DataTemplate), typeof(ProviderGrid));

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

        public ProviderGrid() {
            InitializeComponent();
        }
    }
}
