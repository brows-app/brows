using System.Threading;
using System.Threading.Tasks;

namespace Brows.Gui;

internal interface IFileSystemTerminalController : IController {
    bool HasKeyboardFocus { get; }
    bool RequestFocus();
    void RequestFocusLater(long generation, long requestGeneration, CancellationToken token);
    Task<bool> OpenAsync(CancellationToken token);
    Task<bool> FocusEntriesAsync(long generation, long releaseIntentGeneration, CancellationToken token);
    Task CloseAsync(CancellationToken token);
}
