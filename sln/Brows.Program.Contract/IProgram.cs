using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IProgram {
        string Name { get; }
        Task<int> Run(IProgramContext context, CancellationToken cancellationToken);
    }
}
