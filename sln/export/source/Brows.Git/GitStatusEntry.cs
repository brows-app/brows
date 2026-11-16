using System.Threading.Tasks;

namespace Brows;

internal sealed class GitStatusEntry : Entry<GitStatusProvider> {
    internal async Task<bool> Update(GitStatusRow row, CancellationToken token) {
        if (Row.Status == row.Status && Row.OriginalPath == row.OriginalPath) {
            return false;
        }
        var previous = Row;
        Row = row;
        NotifyPropertyChanged(nameof(Row));
        try {
            await Refresh(token);
        }
        catch {
            Row = previous;
            NotifyPropertyChanged(nameof(Row));
            throw;
        }
        return true;
    }

    public GitStatusRow Row { get; private set; }
    public sealed override string ID => Row.Path;
    public sealed override string Name => Row.Path;

    public GitStatusEntry(GitStatusProvider provider, GitStatusRow row) : base(provider) {
        Row = row ?? throw new ArgumentNullException(nameof(row));
    }
}
