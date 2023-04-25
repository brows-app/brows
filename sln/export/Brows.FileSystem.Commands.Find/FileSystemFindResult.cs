using Brows.Commands;
using Brows.Gui;
using Domore.Notification;
using System;
using System.Collections.ObjectModel;
using System.IO;

namespace Brows {
    internal sealed class FileSystemFindResult : Notifier, IControlled<IFileSystemFindResultController> {
        private readonly ObservableCollection<FoundInInfo> Collection = new();

        private void Controller_CurrentChanged(object sender, EventArgs e) {
            CurrentItem = Controller?.CurrentItem;
        }

        public object Source =>
            Collection;

        public IFileSystemFindResultController Controller {
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
        private IFileSystemFindResultController _Controller;

        public FoundInInfo CurrentItem {
            get => _CurrentItem;
            private set => Change(ref _CurrentItem, value, nameof(CurrentItem));
        }
        private FoundInInfo _CurrentItem;

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

        public bool Canceled {
            get => _Canceled;
            set => Change(ref _Canceled, value, nameof(Canceled));
        }
        private bool _Canceled;

        public Exception Exception {
            get => _Exception;
            set => Change(ref _Exception, value, nameof(Exception));
        }
        private Exception _Exception;

        public string Conf { get; }
        public string Input { get; }
        public DirectoryInfo Root { get; }
        public FindParameter Parameter { get; }

        public FileSystemFindResult(DirectoryInfo root, FindParameter parameter, string input, string conf) {
            Root = root;
            Conf = conf;
            Input = input;
            Parameter = parameter;
        }

        public void Add(FoundInInfo[] item) {
            if (item != null && item.Length > 0) {
                foreach (var i in item) {
                    Collection.Add(i);
                }
            }
        }
    }
}
