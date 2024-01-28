using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Brows.Exports {
    internal sealed class ClipboardSetIO : IClipboardSetIO {
        private readonly Win32ClipboardData ClipboardData = Win32ClipboardData.Instance;

        public async Task<bool> Work(IEnumerable<IProvidedIO> collection, IOperationProgress progress, CancellationToken token) {
            if (collection is null) return false;
            var files = collection.FileSources();
            var streamSets = collection.StreamSets();
            if (streamSets.Count == 0 && files.Count == 0) {
                return false;
            }
            Clipboard.Clear();
            ClipboardData.Clear();
            var
            fileDropList = new StringCollection();
            foreach (var f in files) {
                fileDropList.Add(f);
            }
            if (fileDropList.Count > 0) {
                Clipboard.SetFileDropList(fileDropList);
            }
            ClipboardData.Add(streamSets);
            await Task.CompletedTask;
            return true;
        }
    }
}
