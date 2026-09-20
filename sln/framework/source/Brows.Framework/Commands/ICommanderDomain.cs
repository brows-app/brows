using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Commands;

public interface ICommanderDomain {
    Task<bool> AddCommander(IReadOnlyList<string> panels, CancellationToken token);
}
