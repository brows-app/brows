using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class LinkFile : ILinkFile {
        public async Task<bool> Link(string file, StringBuilder link, CancellationToken token) {
            if (link is null) {
                return false;
            }
            var resolve = await Win32FileLink.Resolve(file, token);
            if (resolve is null) {
                return false;
            }
            link.Append(resolve);
            return true;
        }
    }
}
