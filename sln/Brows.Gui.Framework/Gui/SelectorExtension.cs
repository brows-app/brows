using System;
using System.Windows.Controls.Primitives;

namespace Brows.Gui {
    public static class SelectorExtension {
        public static bool SelectPrevious(this Selector selector) {
            if (null == selector) throw new ArgumentNullException(nameof(selector));
            var i = selector.SelectedIndex;
            if (i < 0) {
                if (selector.Items.Count > 0) {
                    selector.SelectedIndex = 0;
                    return true;
                }
            }
            if (i > 0) {
                selector.SelectedIndex = i - 1;
                return true;
            }
            return false;
        }

        public static bool SelectNext(this Selector selector) {
            if (null == selector) throw new ArgumentNullException(nameof(selector));
            var i = selector.SelectedIndex;
            if (i < 0) {
                if (selector.Items.Count > 0) {
                    selector.SelectedIndex = 0;
                    return true;
                }
            }
            else {
                if (i < (selector.Items.Count - 1)) {
                    selector.SelectedIndex = i + 1;
                    return true;
                }
            }
            return false;
        }

        public static bool SelectOffset(this Selector selector, int offset) {
            if (null == selector) throw new ArgumentNullException(nameof(selector));
            var i = selector.SelectedIndex;
            if (i < 0) {
                i = 0;
            }
            var j = i + offset;
            if (j < 0) {
                j = 0;
            }
            if (j >= selector.Items.Count) {
                j = selector.Items.Count - 1;
            }
            if (j >= 0) {
                selector.SelectedIndex = j;
                return true;
            }
            return false;
        }
    }
}
