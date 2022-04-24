using System.Collections.Generic;

namespace Brows {
    public interface IClipboard {
        public void SetFileDropList(IEnumerable<string> collection);
        public IEnumerable<string> GetFileDropList();
    }
}
