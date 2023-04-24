using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;

namespace Brows.Gui {
    internal sealed class PanelDrop : IPanelDrop {
        private IEnumerable<string> GetFiles() {
            var data = Drag.Data;
            if (data == null) yield break;
            if (data.GetDataPresent(DataFormats.FileDrop)) {
                var collection = data.GetData(DataFormats.FileDrop) as IEnumerable;
                if (collection != null) {
                    foreach (var item in collection) {
                        yield return Convert.ToString(item);
                    }
                }
            }
        }

        private IReadOnlyList<string> Files =>
            _Files ?? (
            _Files = new List<string>(GetFiles()));
        private IReadOnlyList<string> _Files;

        public IReadOnlyList<string> MoveFiles =>
            Drag.KeyStates.HasFlag(DragDropKeyStates.ControlKey)
                ? Array.Empty<string>()
                : Files;

        public IReadOnlyList<string> CopyFiles =>
            Drag.KeyStates.HasFlag(DragDropKeyStates.ControlKey)
                ? Files
                : Array.Empty<string>();

        public DragEventArgs Drag { get; }

        public PanelDrop(DragEventArgs drag) {
            Drag = drag ?? throw new ArgumentNullException(nameof(drag));
        }
    }
}
