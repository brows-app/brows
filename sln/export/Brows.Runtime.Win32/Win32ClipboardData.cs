using System.Collections.Generic;
using System.Linq;

namespace Brows {
    internal sealed class Win32ClipboardData {
        private readonly List<object> List = new();

        private Win32ClipboardData() {
        }

        public static readonly Win32ClipboardData Instance = new();

        public IEnumerable<T> Get<T>() {
            return List.OfType<T>();
        }

        public void Add<T>(IEnumerable<T> list) {
            List.AddRange(list.OfType<object>());
        }

        public void Clear() {
            List.Clear();
        }
    }
}
