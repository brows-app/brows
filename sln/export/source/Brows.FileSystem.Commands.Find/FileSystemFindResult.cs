using Brows.Commands;
using Brows.Gui;
using Domore.Notification;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Threading;

namespace Brows;

internal sealed class FileSystemFindResult : Notifier, IControlled<IFileSystemFindResultController> {
    private readonly ResultCollection Collection = new();
    private IFileSystemFindResultController Controller;

    private void Controller_CurrentChanged(object sender, EventArgs e) {
        if (ReferenceEquals(sender, Controller)) {
            CurrentItem = Controller?.CurrentItem;
        }
    }

    private sealed class ResultCollection : ObservableCollection<FoundInInfo>, ISyncedCollection {
        private readonly Lock Locker = new();

        internal void AddRange(FoundInInfo[] items) {
            lock (Locker) {
                foreach (var item in items) {
                    Items.Add(item);
                }
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        void ISyncedCollection.Sync(IEnumerable collection, object context, Action accessMethod, bool writeAccess) {
            lock (Locker) {
                accessMethod();
            }
        }
    }

    public object Source =>
        Collection;

    public FoundInInfo CurrentItem {
        get;
        private set => Change(ref field, value, nameof(CurrentItem));
    }

    public long MatchTried {
        get;
        set => Change(ref field, value, nameof(MatchTried));
    }

    public long MatchMatched {
        get;
        set => Change(ref field, value, nameof(MatchMatched));
    }

    public long ErrorCount {
        get;
        set => Change(ref field, value, nameof(ErrorCount));
    }

    public long SkippedCount {
        get;
        set => Change(ref field, value, nameof(SkippedCount));
    }

    public bool Truncated {
        get;
        set => Change(ref field, value, nameof(Truncated));
    }

    public bool Complete {
        get;
        set => Change(ref field, value, nameof(Complete));
    }

    public bool Canceled {
        get;
        set {
            if (Change(ref field, value, nameof(Canceled))) {
                var failed = Exception is not null && !value;
                Failed = failed;
            }
        }
    }

    public Exception Exception {
        get;
        set {
            if (Change(ref field, value, nameof(Exception))) {
                var failed = value is not null && !Canceled;
                Failed = failed;
            }
        }
    }

    public bool Failed {
        get;
        private set => Change(ref field, value, nameof(Failed));
    }

    public string Conf { get; }
    public string Input { get; }
    public DirectoryInfo Root { get; }
    public Find.Parameter Parameter { get; }
    public ITranslation Translation { get; init; }

    public FileSystemFindResult(DirectoryInfo root, Find.Parameter parameter, string input, string conf) {
        Root = root;
        Conf = conf;
        Input = input;
        Parameter = parameter;
    }

    public void Add(FoundInInfo[] item) {
        if (item is { Length: > 0 }) {
            Collection.AddRange(item);
        }
    }

    IFileSystemFindResultController IControlled<IFileSystemFindResultController>.Controller {
        set {
            var newValue = value;
            var oldValue = Controller;
            if (oldValue != newValue) {
                if (oldValue is not null) {
                    oldValue.CurrentChanged -= Controller_CurrentChanged;
                }
                Controller = newValue;
                if (newValue is not null) {
                    newValue.CurrentChanged += Controller_CurrentChanged;
                    CurrentItem = newValue.CurrentItem;
                }
                else {
                    CurrentItem = null;
                }
            }
        }
    }
}
