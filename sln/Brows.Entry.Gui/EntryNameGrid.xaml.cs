using System.Windows;

namespace Brows {
    partial class EntryNameGrid {
        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(object), typeof(EntryNameGrid));
        public static readonly DependencyProperty IconTemplateProperty = DependencyProperty.Register(nameof(IconTemplate), typeof(DataTemplate), typeof(EntryNameGrid));

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(object), typeof(EntryNameGrid));
        public static readonly DependencyProperty TextTemplateProperty = DependencyProperty.Register(nameof(TextTemplate), typeof(DataTemplate), typeof(EntryNameGrid));

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

        public EntryNameGrid() {
            InitializeComponent();
            IconContent.DataContext = this;
            TextContent.DataContext = this;
        }
    }
}
