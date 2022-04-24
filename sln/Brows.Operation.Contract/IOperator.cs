using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IOperator {
        Task Deploy(CancellationToken cancellationToken);
    }
}
