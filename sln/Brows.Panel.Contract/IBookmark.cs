using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IBookmark {
        Task<bool> Exists(string value, CancellationToken cancellationToken);
        Task<KeyValuePair<string, string>> MakeFrom(string value, IEnumerable<KeyValuePair<string, string>> existing, CancellationToken cancellationToken);
    }
}
