using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Brows.Exports {
    internal sealed class ClipboardSetIO : IClipboardSetIO {
        private readonly ClipboardData ClipboardData = ClipboardData.Instance;

        public async Task<bool> Work(IEnumerable<IProvidedIO> collection, IClipboardSetIOData data, IOperationProgress progress, CancellationToken token) {
            if (collection is null) return false;
            var files = collection.FileSources();
            var streamSets = collection.StreamSets();
            if (streamSets.Count == 0 && files.Count == 0) {
                return false;
            }
            Clipboard.Clear();
            ClipboardData.Reset();
            ClipboardData.PreferredDropEffect = data?.MoveOnPaste == true
                ? DragDropEffects.Move
                : DragDropEffects.Copy;
            if (files.Count > 0) {
                var fileArr = files.ToArray();
                var fileLst = new StringCollection();
                fileLst.AddRange(fileArr);
                Clipboard.SetFileDropList(fileLst);
            }
            await Task.CompletedTask;
            return true;
        }
    }
}
