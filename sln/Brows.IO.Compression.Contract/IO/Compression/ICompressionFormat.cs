using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO.Compression {
    public interface ICompressionFormat {
        string Extension { get; }
        Task Create(ICompressionCreate state, CancellationToken cancellationToken);
    }
}
