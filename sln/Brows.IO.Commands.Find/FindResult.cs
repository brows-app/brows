using System;

namespace Brows {
    using Collections.ObjectModel;
    using Gui;

    internal class FindResult : CollectionSource<FindItem>, IControlled<IFindResultController> {
        private void Controller_CurrentChanged(object sender, EventArgs e) {
            CurrentItem = Controller?.CurrentItem;
        }

        public IFindResultController Controller {
            get => _Controller;
            set {
                var oldValue = _Controller;
                var newValue = value;
                if (Change(ref _Controller, newValue, nameof(Controller))) {
                    if (oldValue != null) {
                        oldValue.CurrentChanged -= Controller_CurrentChanged;
                    }
                    if (newValue != null) {
                        newValue.CurrentChanged += Controller_CurrentChanged;
                    }
                }
            }
        }

        private IFindResultController _Controller;

        public FindItem CurrentItem {
            get => _CurrentItem;
            private set => Change(ref _CurrentItem, value, nameof(CurrentItem));
        }
        private FindItem _CurrentItem;

        public long MatchTried {
            get => _MatchTried;
            set => Change(ref _MatchTried, value, nameof(MatchTried));
        }
        private long _MatchTried;

        public long MatchMatched {
            get => _MatchMatched;
            set => Change(ref _MatchMatched, value, nameof(MatchMatched));
        }
        private long _MatchMatched;

        public bool Complete {
            get => _Complete;
            set => Change(ref _Complete, value, nameof(Complete));
        }
        private bool _Complete;

        public string Root { get; }
        public string Input { get; }

        public FindResult(string input, string root) {
            Root = root;
            Input = input;
        }

        public bool Add(FindItem item) {
            if (item != null) {
                List.Add(item);
                return true;
            }
            return false;
        }
    }
}
