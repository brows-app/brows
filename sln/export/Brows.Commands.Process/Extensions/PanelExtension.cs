using Domore.IO;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Extensions {
    internal static class PanelExtension {
        public static async Task<string> WorkingDirectory(this IPanel panel, CancellationToken cancellationToken) {
            if (null == panel) throw new ArgumentNullException(nameof(panel));
            if (panel.HasProvider(out IProvider provider) == false) {
                return null;
            }
            var id = provider.ID;
            var exists = null != await FileSystemTask.ExistingDirectory(id, cancellationToken);
            if (exists) {
                return id;
            }
            return null;
        }
    }
}
