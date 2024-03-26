using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Brows.Exports {
    internal sealed class ClipboardGetIO : IClipboardGetIO {
        private readonly ClipboardData ClipboardData = ClipboardData.Instance;

        private async Task<bool> MoveOnPaste(CancellationToken token) {
            await Task.CompletedTask;
            return ClipboardData.PreferredDropEffect == DragDropEffects.Move;
        }

        private static async Task<IReadOnlyList<string>> FileDropList(CancellationToken token) {
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
            await Task.CompletedTask;
            return files;
        }

        public async Task<bool> Work(ICollection<IProvidedIO> collection, IClipboardGetIOData data, IOperationProgress progress, CancellationToken token) {
            if (collection == null) {
                return false;
            }
            var files = await FileDropList(token);
            if (files.Count > 0) {
                if (data != null) {
                    var moveOnPaste = data.MoveOnPaste = await MoveOnPaste(token);
                    if (moveOnPaste) {
                        Clipboard.Clear();
                        ClipboardData.Reset();
                    }
                }
            }
            var f = files.Count > 0 ? [EntryStreamSet.FromFiles(files)] : Array.Empty<IEntryStreamSet>();
            if (f.Length == 0) {
                return false;
            }
            collection.Add(new ProvidedIO {
                StreamSets = f
            });
            return true;
        }
    }
}
