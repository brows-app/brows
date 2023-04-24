using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Brows.Exports {
    internal sealed class ClipboardGetIO : IClipboardGetIO {
        private readonly Win32ClipboardData ClipboardData = Win32ClipboardData.Instance;

        public async Task<bool> Work(ICollection<IProvidedIO> collection, IOperationProgress progress, CancellationToken token) {
            if (collection == null) {
                return false;
            }
            var files = new List<string>();
            var filesDropped = Clipboard.ContainsFileDropList();
            if (filesDropped) {
                var fileDropList = Clipboard.GetFileDropList();
                if (fileDropList != null) {
                    foreach (var file in fileDropList) {
                        if (file != null) {
                            files.Add(file);
                        }
                    }
                }
            }
            var s = ClipboardData.Get<IEntryStreamSet>().Where(s => s is not null).ToList();
            var f = files.Count > 0 ? new[] { EntryStreamSet.FromFiles(files) } : Array.Empty<IEntryStreamSet>();
            var a = s.Concat(f).ToList();
            if (a.Count == 0) {
                return false;
            }
            collection.Add(new ProvidedIO {
                StreamSets = a
            });
            await Task.CompletedTask;
            return true;
        }
    }
}
