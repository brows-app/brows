using System.Collections.Generic;

namespace Brows {
    public interface IPayload {
        IEnumerable<string> CopyFiles { get; }
        IEnumerable<string> MoveFiles { get; }
        IEnumerable<IEntry> CopyEntries { get; }
        IEnumerable<IEntry> MoveEntries { get; }
        IEnumerable<IEntry> DeleteEntries { get; }
        IEnumerable<IEntry> RenameEntries { get; }
        IEnumerable<string> CreateFiles { get; }
        IEnumerable<string> CreateDirectories { get; }
        bool NativeError { get; }
        bool NativeProgress { get; }
        bool NativeConfirmation { get; }
        bool NativeFailEarly { get; }
        bool NativeUndo { get; }
        bool NativeTrash { get; }
        bool NativeRenameOnCollision { get; }
    }
}
