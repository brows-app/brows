using System.Windows;
using System.Windows.Controls;

namespace Brows {
    partial class CommandSuggestionContentControl {
        protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved) {
            var fe = visualAdded as FrameworkElement;
            if (fe != null) {
                var resources = fe.Resources;
                if (resources != null) {
                    if (resources.IsReadOnly == false) {
                        resources.Add(typeof(TextBlock), TryFindResource(typeof(TextBlock)));
                    }
                }
            }
            base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        }

        public CommandSuggestionContentControl() {
            InitializeComponent();
        }
    }
}
