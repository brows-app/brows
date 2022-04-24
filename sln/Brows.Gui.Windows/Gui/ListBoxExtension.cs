using System;
using System.Windows.Controls;

namespace Brows.Gui {
    public static class ListBoxExtension {
        private static bool MoveToSelected(this ListBox listBox) {
            if (null == listBox) throw new ArgumentNullException(nameof(listBox));
            listBox.ScrollIntoView(listBox.SelectedItem);
            return listBox.Items.MoveCurrentTo(listBox.SelectedItem);
        }

        public static bool MoveToPreviousOne(this ListBox listBox) {
            if (null == listBox) throw new ArgumentNullException(nameof(listBox));
            var selected = SelectorExtension.SelectPrevious(listBox);
            return selected
                ? listBox.MoveToSelected()
                : false;
        }

        public static bool MoveToNextOne(this ListBox listBox) {
            if (null == listBox) throw new ArgumentNullException(nameof(listBox));
            var selected = SelectorExtension.SelectNext(listBox);
            return selected
                ? listBox.MoveToSelected()
                : false;
        }
    }
}
