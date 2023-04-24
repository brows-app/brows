using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Windows;

namespace Brows.Exports {
    internal sealed class DragSourceFileSystemInfos : IDragSourceFileSystemInfos {
        public void Drag(object source, IEnumerable<FileSystemInfo> fileSystemInfos) {
            if (fileSystemInfos == null) {
                return;
            }
            var obj = source as DependencyObject;
            if (obj == null) {
                return;
            }
            var fileDropList = new StringCollection();
            foreach (var item in fileSystemInfos) {
                if (item != null) {
                    fileDropList.Add(item.FullName);
                }
            }
            if (fileDropList.Count > 0) {
                var data = new DataObject();
                data.SetFileDropList(fileDropList);
                DragDrop.DoDragDrop(obj, data, DragDropEffects.Copy | DragDropEffects.Move);
            }
        }
    }
}
