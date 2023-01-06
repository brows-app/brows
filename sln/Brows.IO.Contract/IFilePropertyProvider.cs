using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IFilePropertyProvider {
        IReadOnlyList<string> PropertyKeys { get; }
        IAsyncEnumerable<IFileProperty> GetProperties(string file, IEnumerable<string> keys, CancellationToken cancellationToken);
        Task SetProperties(string file, IEnumerable<IFileProperty> properties, CancellationToken cancellationToken);
    }
}
