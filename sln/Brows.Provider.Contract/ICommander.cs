using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface ICommander {
        bool HasOperations(out IOperationCollection collection);
        Task<bool> AddPanel(string id, CancellationToken token);
        Task<bool> ShiftPanel(IPanel panel, int column, CancellationToken token);
        Task<bool> RemovePanel(IPanel panel, CancellationToken token);
        Task<bool> ClearPanels(CancellationToken token);
    }
}
