using System.Windows;
using System.Windows.Media;

namespace Brows.Gui {
    public static class DependencyObjectExtension {
        public static T FindVisualChild<T>(this DependencyObject depObj) where T : class {
            var childCount = VisualTreeHelper.GetChildrenCount(depObj);
            for (int i = 0; i < childCount; i++) {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t) {
                    return t;
                }
                var ancestor = FindVisualChild<T>(child);
                if (ancestor != null) {
                    return ancestor;
                }
            }
            return null;
        }
    }
}
