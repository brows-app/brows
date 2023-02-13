using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IProgram {
        string Name { get; }
        Task<int> Run(IProgramCommand command, IProgramConsole console, CancellationToken cancellationToken);
    }
}
