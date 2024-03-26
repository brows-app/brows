using System.Windows;

namespace Brows.Exports {
    internal sealed class ClipboardData {
        public static ClipboardData Instance { get; } = new ClipboardData();

        public DragDropEffects PreferredDropEffect { get; set; }

        public ClipboardData() {
            Reset();
        }

        public void Reset() {
            PreferredDropEffect = DragDropEffects.Copy;
        }
    }
}
