using Brows.Win32;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Exports;

internal sealed class DetailFileSystemInfos : IDetailFileSystemInfos {
    [ImportRequired]
    internal IWin32InteropServices Service { get; set; }

    public Task<bool> Work(IEnumerable<FileSystemInfo> fileSystemInfos, IOperationProgress progress, CancellationToken token) {
        if (fileSystemInfos != null) {
            var list = fileSystemInfos.Where(info => info != null).ToList();
            if (list.Count > 0) {
                if (progress != null) {
                    progress.Change(setTarget: list.Count);
                }
                return Task.Run(cancellationToken: token, function: async () => {
                    foreach (var item in list) {
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        if (progress != null) {
                            progress.Change(data: item.Name);
                        }
                        await Service.ExecuteProperties(item.FullName);
                        if (progress != null) {
                            progress.Change(addProgress: 1);
                        }
                    }
                    return true;
                });
            }
        }
        return Task.FromResult(false);
    }
}
