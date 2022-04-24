using System;
using System.Linq;

namespace Brows.Gui {
    using Collections.ObjectModel;

    public class Logbook : CollectionSource<LogItem>, IControlled<ILogbookController> {
        private double? Offset;

        public ILogbookController Controller {
            get => _Controller;
            set {
                var oldValue = _Controller;
                var newValue = value;
                if (Change(ref _Controller, newValue, nameof(Controller))) {
                }
            }
        }
        private ILogbookController _Controller;

        public void Add(LogItem item) {
            List.Add(item);
        }

        public void Clear() {
            List.Clear();
        }

        public void ScrollUp() {
            Offset = Controller?.ScrollUp();
        }

        public void ScrollDown() {
            Offset = Controller?.ScrollDown();
        }

        public void PageUp() {
            Offset = Controller?.PageUp();
        }

        public void PageDown() {
            Offset = Controller?.PageDown();
        }

        public override string ToString() {
            return string.Join(Environment.NewLine, List.Select(item => item.Message));
        }
    }
}
