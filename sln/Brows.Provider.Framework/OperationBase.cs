using Domore.Logs;
using Domore.Notification;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TASK = System.Threading.Tasks.Task;

namespace Brows {
    internal class OperationBase : Notifier, IOperation {
        private static readonly ILog Log = Logging.For(typeof(OperationBase));
        private static readonly PropertyChangedEventArgs CancelingEvent = new(nameof(Canceling));
        private static readonly PropertyChangedEventArgs CompleteEvent = new(nameof(Complete));
        private static readonly PropertyChangedEventArgs CompleteWithErrorEvent = new(nameof(CompleteWithError));
        private static readonly PropertyChangedEventArgs DataEvent = new(nameof(Data));
        private static readonly PropertyChangedEventArgs ErrorEvent = new(nameof(Error));
        private static readonly PropertyChangedEventArgs ErrorMessageEvent = new(nameof(ErrorMessage));
        private static readonly PropertyChangedEventArgs NameEvent = new(nameof(Name));
        private static readonly PropertyChangedEventArgs ProgressEvent = new(nameof(Progress));
        private static readonly PropertyChangedEventArgs ProgressingEvent = new(nameof(Progressing));
        private static readonly PropertyChangedEventArgs ProgressKindEvent = new(nameof(ProgressKind));
        private static readonly PropertyChangedEventArgs ProgressPercentEvent = new(nameof(ProgressPercent));
        private static readonly PropertyChangedEventArgs ProgressStringEvent = new(nameof(ProgressString));
        private static readonly PropertyChangedEventArgs RelevantEvent = new(nameof(Relevant));
        private static readonly PropertyChangedEventArgs TargetEvent = new(nameof(Target));
        private static readonly PropertyChangedEventArgs TargetStringEvent = new(nameof(TargetString));
        private static readonly PropertyChangedEventArgs[] ErrorDependents = [ErrorMessageEvent];
        private static readonly PropertyChangedEventArgs[] ProgressDependents = [ProgressPercentEvent, ProgressStringEvent];
        private static readonly PropertyChangedEventArgs[] ProgressKindDependents = [ProgressStringEvent, TargetStringEvent];
        private static readonly PropertyChangedEventArgs[] TargetDependents = [ProgressPercentEvent, TargetStringEvent];

        private bool MakingRelevant;
        private Stopwatch Stopwatch;
        private CancellationTokenSource TokenSource;
        private readonly OperationBaseCollection ChildCollection = new();
        private readonly object TokenLocker = new();
        private readonly object ProgressLocker = new();
        private readonly object RelevantLocker = new();

        private OperationBase Child(string name, OperationProgressKind progressKind, OperationDelegate task) {
            if (Log.Info()) {
                Log.Info(nameof(Child) + " > " + name);
            }
            var child = new OperationBase(name, progressKind, this, task);
            ChildCollection.Add(child);
            return child;
        }

        private async void MakeRelevant() {
            if (Relevant) return;
            if (MakingRelevant) return;
            lock (RelevantLocker) {
                if (MakingRelevant) {
                    return;
                }
                MakingRelevant = true;
            }
            var token = default(CancellationToken);
            lock (TokenLocker) {
                if (TokenSource != null) {
                    token = TokenSource.Token;
                }
            }
            try {
                await TASK.Delay(1000, token);
            }
            catch (OperationCanceledException canceled) when (canceled.CancellationToken == token) {
            }
            if (Progressing) {
                Relevant = true;
            }
            MakingRelevant = false;
        }

        private void NotifyTargetChanged() {
            NotifyPropertyChanged(TargetEvent, TargetDependents);
            TargetChanged?.Invoke(this, EventArgs.Empty);
            MakeRelevant();
        }

        private void NotifyProgressChanged() {
            NotifyPropertyChanged(ProgressEvent, ProgressDependents);
            ProgressChanged?.Invoke(this, EventArgs.Empty);
            MakeRelevant();
        }

        private void ChangeProgress(long? setProgress = null, long? addProgress = null, long? setTarget = null, long? addTarget = null) {
            var changed = false;
            var changedTarget = false;
            var changedProgress = false;
            lock (ProgressLocker) {
                if (setProgress.HasValue) {
                    var
                    value = setProgress.Value;
                    changed = true;
                    changedProgress = true;
                    if (Parent != null) {
                        Parent.ChangeProgress(setProgress: Parent.Progress + (value - Progress));
                    }
                    Progress = value;
                }
                if (addProgress.HasValue) {
                    var
                    value = addProgress.Value;
                    changed = true;
                    changedProgress = true;
                    if (Parent != null) {
                        Parent.ChangeProgress(addProgress: value);
                    }
                    Progress += value;
                }
                if (setTarget.HasValue) {
                    var
                    value = setTarget.Value;
                    changed = true;
                    changedTarget = true;
                    if (Parent != null) {
                        Parent.ChangeProgress(setTarget: Parent.Target + (value - Target));
                    }
                    Target = value;
                }
                if (addTarget.HasValue) {
                    var
                    value = addTarget.Value;
                    changed = true;
                    changedTarget = true;
                    if (Parent != null) {
                        Parent.ChangeProgress(addTarget: value);
                    }
                    Target += value;
                }
                if (changed) {
                    ProgressPercent = Target == 0
                        ? 0
                        : (double)Progress / Target * 100;
                }
            }
            if (changedTarget) {
                NotifyTargetChanged();
            }
            if (changedProgress) {
                NotifyProgressChanged();
            }
        }

        private async Task Operate() {
            if (Log.Info()) {
                Log.Info(nameof(Operate));
            }
            var tokenSource = default(CancellationTokenSource);
            lock (TokenLocker) {
                tokenSource = TokenSource = new();
            }
            using (tokenSource) {
                Progressing = true;
                Stopwatch = Stopwatch.StartNew();
                var token = tokenSource.Token;
                var progress = new ProgressWrapper(this);
                try {
                    await Task(progress, token);
                    await TASK.WhenAll(ChildCollection.Select(child => child.Completion));
                }
                catch (Exception ex) {
                    if (ex is OperationCanceledException canceled && canceled.CancellationToken == token) {
                        if (Log.Info()) {
                            Log.Info(canceled.GetType());
                        }
                    }
                    else {
                        if (Log.Warn()) {
                            Log.Warn(ex);
                        }
                        Error = ex;
                    }
                }
                Stopwatch.Stop();
                Progressing = false;
                lock (TokenLocker) {
                    TokenSource = null;
                }
            }
            if (Error != null) {
                CompleteWithError = true;
            }
            if (ChildCollection.Any(child => child.CompleteWithError)) {
                CompleteWithError = true;
            }
            if (CompleteWithError) {
                Relevant = true;
            }
            else {
                lock (ProgressLocker) {
                    if (Target == 0) {
                        ProgressPercent = 100;
                        NotifyPropertyChanged(ProgressPercentEvent);
                    }
                }
            }
            Complete = true;
            Completed?.Invoke(this, EventArgs.Empty);
        }

        protected void Cancel() {
            if (Log.Info()) {
                Log.Info(nameof(Cancel) + " > " + Name);
            }
            Canceling = true;
            foreach (var child in ChildCollection) {
                child.Cancel();
            }
            lock (TokenLocker) {
                if (TokenSource != null) {
                    TokenSource.Cancel();
                }
            }
        }

        public event EventHandler RelevantChanged;
        public event EventHandler TargetChanged;
        public event EventHandler ProgressChanged;
        public event EventHandler Completed;

        public long Target { get; private set; }
        public long Progress { get; private set; }
        public double ProgressPercent { get; private set; }
        
        public string TargetString =>
            ProgressKind == OperationProgressKind.FileSize ? EntryDataConverter.FileSize.From(Target, "0.00", null) :
            Target.ToString();

        public string ProgressString =>
            ProgressKind == OperationProgressKind.FileSize ? EntryDataConverter.FileSize.From(Progress, "0.00", null) :
            Progress.ToString();

        public OperationProgressKind ProgressKind {
            get => _ProgressKind;
            private set => Change(ref _ProgressKind, value, ProgressKindEvent, ProgressKindDependents);
        }
        private OperationProgressKind _ProgressKind;

        public string Name {
            get => _Name;
            private set => Change(ref _Name, value, NameEvent);
        }
        private string _Name;

        public string Data {
            get => _Data;
            private set => Change(ref _Data, value, DataEvent);
        }
        private string _Data;

        public bool Relevant {
            get => _Relevant;
            private set {
                if (Change(ref _Relevant, value, RelevantEvent)) {
                    RelevantChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        private bool _Relevant;

        public bool Canceling {
            get => _Canceling;
            private set => Change(ref _Canceling, value, CancelingEvent);
        }
        private bool _Canceling;

        public Exception Error {
            get => _Error;
            private set => Change(ref _Error, value, ErrorEvent, ErrorDependents);
        }
        private Exception _Error;

        public string ErrorMessage => Error == null
            ? (null)
            : (Translation.Global.Value($"Operation_Error_{Error?.GetType()?.Name}") ?? Error?.Message);

        public bool Progressing {
            get => _Progressing;
            private set => Change(ref _Progressing, value, ProgressingEvent);
        }
        private bool _Progressing;

        public object ChildSource => ChildCollection.Source;

        public int Depth { get; }

        public string DepthString =>
            _DepthString ?? (
            _DepthString = string.Create(Depth, default(object), (span, _) => span.Fill('>')));
        private string _DepthString;

        public bool CompleteWithError {
            get => _CompleteWithError;
            private set => Change(ref _CompleteWithError, value, CompleteWithErrorEvent);
        }
        private bool _CompleteWithError;

        public bool Complete {
            get => _Complete;
            private set => Change(ref _Complete, value, CompleteEvent);
        }
        private bool _Complete;

        public Task Completion { get; }
        public OperationBase Parent { get; }
        public OperationDelegate Task { get; }

        public OperationBase(string name, OperationProgressKind progressKind, OperationBase parent, OperationDelegate task) {
            Task = task ?? throw new ArgumentNullException(nameof(task));
            Name = name;
            Parent = parent;
            Depth = Parent == null ? 0 : (Parent.Depth + 1);
            ProgressKind = progressKind;
            Completion = Operate();
        }

        public OperationBase(string name, OperationBase parent, OperationDelegate task) : this(name, OperationProgressKind.None, parent, task) {
        }

        private sealed class ProgressWrapper : IOperationProgress {
            public OperationProgressKind Kind =>
                Operation.ProgressKind;

            public OperationBase Operation { get; }

            public ProgressWrapper(OperationBase operation) {
                Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            }

            public void Change(long? addProgress, long? setProgress, long? addTarget, long? setTarget, string name, string data, OperationProgressKind? kind) {
                if (kind != null) {
                    Operation.ProgressKind = kind.Value;
                }
                if (name != null) {
                    Operation.Name = name;
                }
                if (data != null) {
                    Operation.Data = data;
                }
                Operation.ChangeProgress(addProgress: addProgress, setProgress: setProgress, addTarget: addTarget, setTarget: setTarget);
            }

            public void Child(string name, OperationProgressKind kind, OperationDelegate task) {
                Operation.Child(name, kind, task);
            }
        }
    }
}
