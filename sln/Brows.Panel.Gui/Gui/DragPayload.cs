using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;

namespace Brows.Gui {
    internal class DragPayload : IPayload {
        private IEnumerable<string> Files {
            get {
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
        }

        public bool NativeError { get; } = true;
        public bool NativeProgress { get; } = true;
        public bool NativeConfirmation { get; } = true;
        public bool NativeUndo { get; } = true;
        public bool NativeTrash { get; } = true;
        public bool NativeRenameOnCollision { get; } = false;
        public bool NativeFailEarly { get; } = false;

        public IEnumerable<string> MoveFiles =>
            Drag.KeyStates.HasFlag(DragDropKeyStates.ControlKey)
                ? Array.Empty<string>()
                : Files;

        public IEnumerable<string> CopyFiles =>
            Drag.KeyStates.HasFlag(DragDropKeyStates.ControlKey)
                ? Files
                : Array.Empty<string>();

        public IEnumerable<IEntry> CopyEntries => Array.Empty<IEntry>();
        public IEnumerable<IEntry> MoveEntries => Array.Empty<IEntry>();
        public IEnumerable<IEntry> OpenEntries => Array.Empty<IEntry>();
        public IEnumerable<IEntry> DeleteEntries => Array.Empty<IEntry>();
        public IEnumerable<IEntry> RenameEntries => Array.Empty<IEntry>();
        public IEnumerable<string> OpenFiles => Array.Empty<string>();
        public IEnumerable<string> CreateFiles => Array.Empty<string>();
        public IEnumerable<string> CreateDirectories => Array.Empty<string>();

        public DragEventArgs Drag { get; }

        public DragPayload(DragEventArgs drag) {
            Drag = drag ?? throw new ArgumentNullException(nameof(drag));
        }
    }
}
