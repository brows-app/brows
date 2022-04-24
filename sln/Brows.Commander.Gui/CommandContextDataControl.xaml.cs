using System.Windows;
using System.Windows.Controls;

namespace Brows {
    partial class CommandContextDataControl {
        private Application Application =>
            _Application ?? (
            _Application = Application.Current);
        private Application _Application;

        private void This_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            var newValue = e.NewValue;
            var data = newValue as ICommandContextData;
            if (data != null) {
                var resourceKey = $"{nameof(CommandContextData)}_{data.Control}";
                var resource = Application.TryFindResource(resourceKey);
                if (resource != null) {
                    var template = resource as ControlTemplate;
                    if (template != null) {
                        if (ContentControl.Template != template) {
                            ContentControl.Template = template;
                        }
                    }
                    else {
                        ContentControl.Template = null;
                    }
                }
                ContentControl.DataContext = data.Current;
            }
        }

        public CommandContextDataControl() {
            DataContextChanged += This_DataContextChanged;
            InitializeComponent();
        }
    }
}
