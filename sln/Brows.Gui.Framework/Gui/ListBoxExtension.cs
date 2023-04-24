using System;
using System.Windows.Controls;

namespace Brows.Gui {
    public static class ListBoxExtension {
        private static bool MoveToSelected(this ListBox listBox) {
            if (null == listBox) throw new ArgumentNullException(nameof(listBox));
            listBox.ScrollIntoView(listBox.SelectedItem);
            return listBox.Items.MoveCurrentTo(listBox.SelectedItem);
        }

        public static bool MoveUp(this ListBox listBox) {
            if (null == listBox) throw new ArgumentNullException(nameof(listBox));
            var selected = SelectorExtension.SelectPrevious(listBox);
            return selected
                ? listBox.MoveToSelected()
                : false;
        }

        public static bool MoveDown(this ListBox listBox) {
            if (null == listBox) throw new ArgumentNullException(nameof(listBox));
            var selected = SelectorExtension.SelectNext(listBox);
            return selected
                ? listBox.MoveToSelected()
                : false;
        }

        public static bool MovePageUp(this ListBox listBox) {
            if (null == listBox) throw new ArgumentNullException(nameof(listBox));
            var scrollViewer = listBox.FindVisualChild<ScrollViewer>();
            if (scrollViewer != null) {
                return MoveToOffset(listBox, -(int)scrollViewer.ViewportHeight);
            }
            return false;
        }

        public static bool MovePageDown(this ListBox listBox) {
            if (null == listBox) throw new ArgumentNullException(nameof(listBox));
            var scrollViewer = listBox.FindVisualChild<ScrollViewer>();
            if (scrollViewer != null) {
                return MoveToOffset(listBox, (int)scrollViewer.ViewportHeight);
            }
            return false;
        }

        public static bool MoveToOffset(this ListBox listBox, int offset) {
            if (null == listBox) throw new ArgumentNullException(nameof(listBox));
            var selected = SelectorExtension.SelectOffset(listBox, offset);
            return selected
                ? listBox.MoveToSelected()
                : false;
        }

        public static bool Move(this ListBox listBox, PressKey pressKey) {
            switch (pressKey) {
                case PressKey.Down:
                    return MoveDown(listBox);
                case PressKey.Up:
                    return MoveUp(listBox);
                case PressKey.PageDown:
                    return MovePageDown(listBox);
                case PressKey.PageUp:
                    return MovePageUp(listBox);
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
