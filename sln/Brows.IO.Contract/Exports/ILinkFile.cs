using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface ILinkFile : IExport {
        Task<bool> Link(string file, StringBuilder link, CancellationToken cancellationToken);
    }
}
