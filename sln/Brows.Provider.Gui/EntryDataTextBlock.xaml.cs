using System.Windows;

namespace Brows {
    using Windows;

    partial class EntryDataTextBlock {
        public static readonly DependencyProperty FlagProperty =
            FlagDependencyProperty.Register(typeof(EntryDataTextBlock), ForegroundProperty);

        public string Flag {
            get => (string)GetValue(FlagProperty);
            set => SetValue(FlagProperty, value);
        }

        public EntryDataTextBlock() {
            InitializeComponent();
        }
    }
}
