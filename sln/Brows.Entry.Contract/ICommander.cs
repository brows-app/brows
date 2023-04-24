using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface ICommander {
        bool HasOperations(out IOperationCollection collection);
        Task<bool> AddPanel(string id, CancellationToken token);
        Task<bool> RemovePanel(IPanel panel, CancellationToken token);
        Task<bool> ClearPanels(CancellationToken token);
        Task<bool> ShowPalette(string input, CancellationToken token);
        Task<bool> ShowPalette(string input, int selectedStart, int selectedLength, CancellationToken token);
    }
}
