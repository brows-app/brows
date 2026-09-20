using Brows.Operations;
using Brows.Panels;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

public interface ICommander {
    bool HasOperations(out IOperationCollection collection);
    bool HasWindow(out object native);
    Task<bool> AddPanel(string id, CancellationToken token);
    Task<bool> ShiftPanel(IPanel panel, int column, CancellationToken token);
    Task<bool> RemovePanel(IPanel panel, CancellationToken token);
    Task<bool> ClearPanels(CancellationToken token);
}
