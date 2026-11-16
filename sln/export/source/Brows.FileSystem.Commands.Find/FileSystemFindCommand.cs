using System.Collections.Generic;

namespace Brows;

internal sealed class FileSystemFindCommand {
    private const int MaxCompletedResults = 20;

    public IList<FileSystemFindResult> List { get; } = new List<FileSystemFindResult>();

    public void PruneCompleted() {
        var completed = 0;
        for (var i = 0; i < List.Count; i++) {
            if (List[i].Complete) {
                completed++;
                if (completed > MaxCompletedResults) {
                    List.RemoveAt(i);
                    i--;
                }
            }
        }
    }
}
