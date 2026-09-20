using Brows.Composition;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Messaging;

public interface IMessageFactory : IExport {
    Task<bool> Work(object window, ICollection<IMessenger> set, CancellationToken token);
}
