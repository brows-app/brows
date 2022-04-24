using System;
using System.Collections.Generic;

namespace Brows {
    internal class PanelPayload : IPayload {
        public bool NativeError { get; set; } = true;
        public bool NativeProgress { get; set; } = true;
        public bool NativeConfirmation { get; set; } = true;
        public bool NativeUndo { get; set; } = true;
        public bool NativeTrash { get; set; } = true;
        public bool NativeRenameOnCollision { get; set; }
        public bool NativeFailEarly { get; set; }

        public IEnumerable<string> CopyFiles {
            get => _CopyFiles ?? (_CopyFiles = Array.Empty<string>());
            set => _CopyFiles = value;
        }
        private IEnumerable<string> _CopyFiles;

        public IEnumerable<string> MoveFiles {
            get => _MoveFiles ?? (_MoveFiles = Array.Empty<string>());
            set => _MoveFiles = value;
        }
        private IEnumerable<string> _MoveFiles;

        public IEnumerable<string> CreateFiles {
            get => _CreateFiles ?? (_CreateFiles = Array.Empty<string>());
            set => _CreateFiles = value;
        }
        private IEnumerable<string> _CreateFiles;

        public IEnumerable<string> CreateDirectories {
            get => _CreateDirectories ?? (_CreateDirectories = Array.Empty<string>());
            set => _CreateDirectories = value;
        }
        private IEnumerable<string> _CreateDirectories;

        public IEnumerable<IEntry> CopyEntries {
            get => _CopyEntries ?? (_CopyEntries = Array.Empty<IEntry>());
            set => _CopyEntries = value;
        }
        private IEnumerable<IEntry> _CopyEntries;

        public IEnumerable<IEntry> MoveEntries {
            get => _MoveEntries ?? (_MoveEntries = Array.Empty<IEntry>());
            set => _MoveEntries = value;
        }
        private IEnumerable<IEntry> _MoveEntries;

        public IEnumerable<IEntry> DeleteEntries {
            get => _DeleteEntries ?? (_DeleteEntries = Array.Empty<IEntry>());
            set => _DeleteEntries = value;
        }
        private IEnumerable<IEntry> _DeleteEntries;

        public IEnumerable<IEntry> RenameEntries {
            get => _RenameEntries ?? (_RenameEntries = Array.Empty<IEntry>());
            set => _RenameEntries = value;
        }
        private IEnumerable<IEntry> _RenameEntries;
    }
}
