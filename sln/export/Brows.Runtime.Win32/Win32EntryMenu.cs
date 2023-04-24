//using System;
//using System.IO;

//namespace Brows {
//    internal class Win32EntryMenu {
//        public void Show(IEntry entry, double x, double y) {
//            if (null == entry) throw new ArgumentNullException(nameof(entry));

//            var file = new FileInfo(entry.ID);
//            if (file.Exists) {
//                var menu = new Win32ContextMenu();
//                menu.ShowContextMenu(new[] { file }, (int)x, (int)y);
//            }

//            var directory = new DirectoryInfo(entry.ID);
//            if (directory.Exists) {
//                var menu = new Win32ContextMenu();
//                menu.ShowContextMenu(new[] { directory }, (int)x, (int)y);
//            }
//        }
//    }
//}
