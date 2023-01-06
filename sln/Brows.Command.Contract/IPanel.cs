using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IPanel {
        int Column { get; }
        bool Active { get; }
        bool Activated { get; }
        string Directory { get; }
        double ControlWidth { get; }
        double ControlHeight { get; }
        IPanelID ID { get; }
        IPanelProvider Provider { get; }
        IEntryCollection Entries { get; }
        PanelPreviewMode PreviewMode { get; set; }
        void Activate();
        bool Input(string text);
        void Reset();
        void Refresh();
        void Save();
        Task Open(string id, CancellationToken cancellationToken);
        Task OpenParent(CancellationToken cancellationToken);
        Task<bool> OpenCreated(string createdName, CancellationToken cancellationToken);
        Task<bool> HistoryBack(CancellationToken cancellationToken);
        Task<bool> HistoryForward(CancellationToken cancellationToken);
        IEnumerable<IEntry> Selection();
        void Operate(IEntryOperation operation, Func<Task> then = null);
        void Operate(string name, Func<IOperationProgress, CancellationToken, Task> task, Func<Task> then = null);
        void Deploy(IPayload payload, Func<Task> then = null);
        void Deploy(
            IEnumerable<string> copyFiles = null,
            IEnumerable<string> moveFiles = null,
            IEnumerable<string> createFiles = null,
            IEnumerable<string> createDirectories = null,
            IEnumerable<IEntry> copyEntries = null,
            IEnumerable<IEntry> moveEntries = null,
            IEnumerable<IEntry> deleteEntries = null,
            IEnumerable<IEntry> renameEntries = null,
            bool? nativeError = null,
            bool? nativeProgress = null,
            bool? nativeConfirmation = null,
            bool? nativeUndo = null,
            bool? nativeTrash = null,
            bool? nativeRenameOnCollision = null,
            bool? nativeFailEarly = null,
            Func<Task> then = null);
    }
}
