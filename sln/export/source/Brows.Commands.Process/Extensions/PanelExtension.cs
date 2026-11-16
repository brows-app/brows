using System.Threading.Tasks;

namespace Brows.Extensions;

internal static class PanelExtension {
    public static async Task<string> WorkingDirectory(this IPanel panel, CancellationToken cancellationToken) {
        if (panel?.HasProvider(out IProvider provider) != true) {
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
