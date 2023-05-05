using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class DetailFileSystemInfos : IDetailFileSystemInfos {
        public async Task<bool> Work(IEnumerable<FileSystemInfo> fileSystemInfos, IOperationProgress progress, CancellationToken token) {
            if (fileSystemInfos != null) {
                var list = fileSystemInfos.Where(info => info != null).ToList();
                if (list.Count > 0) {
                    if (progress != null) {
                        progress.Change(setTarget: list.Count);
                    }
                    await Task.Run(cancellationToken: token, action: () => {
                        foreach (var item in list) {
                            if (token.IsCancellationRequested) {
                                token.ThrowIfCancellationRequested();
                            }
                            if (progress != null) {
                                progress.Change(data: item.Name);
                            }
                            Win32File.Properties(item.FullName);
                            if (progress != null) {
                                progress.Change(1);
                            }
                        }
                    });
                    return true;
                }
            }
            return false;
        }
    }
}
