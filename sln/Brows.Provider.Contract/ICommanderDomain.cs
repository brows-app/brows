using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface ICommanderDomain {
        Task<bool> AddCommander(IReadOnlyList<string> panels, CancellationToken token);
    }
}
