using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class FilePropertyProvider : IFilePropertyProvider {
        public abstract IReadOnlyList<string> PropertyKeys { get; }
        public abstract IAsyncEnumerable<IFileProperty> GetProperties(string file, IEnumerable<string> keys, CancellationToken cancellationToken);
        public abstract Task SetProperties(string file, IEnumerable<IFileProperty> properties, CancellationToken cancellationToken);
    }
}
