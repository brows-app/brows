using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Extensions {
    using IO;

    internal static class PanelExtension {
        public static async Task<string> WorkingDirectory(this IPanel panel, CancellationToken cancellationToken) {
            if (null == panel) throw new ArgumentNullException(nameof(panel));
            var id = panel.ID?.Value;
            var exists = await DirectoryAsync.Exists(id, cancellationToken);
            if (exists) {
                return id;
            }
            return null;
        }
    }
}
