using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IPanel {
        int Column { get; }
        bool Active { get; }
        bool Activated { get; }
        double ControlWidth { get; }
        double ControlHeight { get; }
        IPanelID ID { get; }
        IPanelProvider Provider { get; }
        IEntryCollection Entries { get; }
        PanelPreviewMode PreviewMode { get; set; }
        void Activate();
        bool Input(string text);
        Task Refresh(CancellationToken cancellationToken);
        Task Up(CancellationToken cancellationToken);
        Task Save(CancellationToken cancellationToken);
        Task Open(string id, CancellationToken cancellationToken);
        Task<bool> OpenCreated(string createdName, CancellationToken cancellationToken);
        Task<bool> HistoryBack(CancellationToken cancellationToken);
        Task<bool> HistoryForward(CancellationToken cancellationToken);
        IEnumerable<IEntry> Selection();
        Task Deploy(IPayload payload, CancellationToken cancellationToken, Func<Task> then = null);
        Task Deploy(
            CancellationToken cancellationToken,
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
