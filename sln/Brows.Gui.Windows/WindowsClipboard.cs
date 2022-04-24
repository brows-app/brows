using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;

namespace Brows {
    public class WindowsClipboard : IClipboard {
        public void SetFileDropList(IEnumerable<string> collection) {
            if (null == collection) throw new ArgumentNullException(nameof(collection));

            var strCol = new StringCollection();
            foreach (var item in collection) {
                strCol.Add(item);
            }
            if (strCol.Count > 0) {
                Clipboard.SetFileDropList(strCol);
            }
        }

        public IEnumerable<string> GetFileDropList() {
            return Clipboard
                .GetFileDropList()
                .Cast<string>()
                .ToList();
        }
    }
}
