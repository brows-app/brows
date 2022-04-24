using System;
using System.Diagnostics;
using System.Threading;

namespace Brows {
    using ComponentModel;
    using Logger;

    internal class OperationBase : NotifyPropertyChanged, IOperation {
        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(OperationBase)));
        private ILog _Log;

        private Stopwatch Stopwatch;
        private readonly OperationCollection ChildCollection;

        private OperationBase(OperationInfo info, OperationBase parent) {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            Parent = parent;
            Depth = Parent == null ? 0 : (Parent.Depth + 1);
            ChildCollection = new OperationCollection();
        }

        private OperationBase Child(string name, string descriptionFormat, params string[] descriptionData) {
            if (Log.Info()) {
                Log.Info(
                    nameof(Child),
                    nameof(name) + " > " + name,
                    nameof(descriptionFormat) + " > " + descriptionFormat,
                    nameof(descriptionData) + " > " + descriptionData);
            }
            var info = new OperationInfo(name, descriptionFormat, descriptionData);
            var child = new OperationBase(info, this);
            ChildCollection.Add(child);
            return child;
        }

        private void MakeRelevant() {
            if (Log.Debug()) {
                Log.Debug(nameof(MakeRelevant));
            }
            if (Relevant == false) {
                if (Stopwatch.ElapsedMilliseconds > 1000) {
                    Relevant = true;
                }
            }
        }

        private void SetTarget(long value) {
            if (Log.Debug()) {
                Log.Debug(
                    nameof(SetTarget),
                    nameof(value) + " > " + value);
            }
            var oldTarget = Target;
            var newTarget = value;
            if (Parent != null) {
                //Parent.SetTarget(value);
            }
            Target = newTarget;
            OnTargetChanged(EventArgs.Empty);
            MakeRelevant();
        }

        private void SetProgress(long value) {
            if (Log.Debug()) {
                Log.Debug(
                    nameof(SetProgress),
                    nameof(value) + " > " + value);
            }
            var oldProgress = Progress;
            var newProgress = value;
            if (Parent != null) {
                //Parent.SetProgress(value);
            }
            Progress = newProgress;
            OnProgressChanged(EventArgs.Empty);
            MakeRelevant();
        }

        protected virtual void OnRelevantChanged(EventArgs e) =>
            RelevantChanged?.Invoke(this, e);

        protected virtual void OnTargetChanged(EventArgs e) =>
            TargetChanged?.Invoke(this, e);

        protected virtual void OnProgressChanged(EventArgs e) =>
            ProgressChanged?.Invoke(this, e);

        protected virtual void OnCompleted(EventArgs e) =>
            Completed?.Invoke(this, e);

        public event EventHandler RelevantChanged;
        public event EventHandler TargetChanged;
        public event EventHandler ProgressChanged;
        public event EventHandler Completed;

        public string Name => Info.Name;
        public string Description => Info.Description;
        public OperationBase Parent { get; }

        public bool Relevant {
            get => _Relevant;
            private set {
                if (Change(ref _Relevant, value, nameof(Relevant))) {
                    OnRelevantChanged(EventArgs.Empty);
                }
            }
        }
        private bool _Relevant;

        public IDialogState Dialog {
            get => _Dialog;
            set => Change(ref _Dialog, value, nameof(Dialog));
        }
        private IDialogState _Dialog;

        public bool Canceling {
            get => _Canceling;
            private set => Change(ref _Canceling, value, nameof(Canceling));
        }
        private bool _Canceling;

        public long Target {
            get => _Target;
            private set {
                if (_Target != value) {
                    _Target = value;
                    NotifyPropertyChanged(nameof(Target));
                    NotifyPropertyChanged(nameof(ProgressPercent));
                }
            }
        }
        private long _Target;

        public long Progress {
            get => _Progress;
            private set {
                if (_Progress != value) {
                    _Progress = value;
                    NotifyPropertyChanged(nameof(Progress));
                    NotifyPropertyChanged(nameof(ProgressPercent));
                }
            }
        }
        private long _Progress;

        public double ProgressPercent {
            get => _ProgressPercent ?? (Target == 0
                ? 0
                : (double)Progress / Target * 100);
            private set => Change(ref _ProgressPercent, value, nameof(ProgressPercent));
        }
        private double? _ProgressPercent;

        public Exception Error {
            get => _Error;
            private set => Change(ref _Error, value, nameof(Error));
        }
        private Exception _Error;

        public string ErrorMessage =>
            _Error?.Message;

        public bool Progressing {
            get => _Progressing;
            private set => Change(ref _Progressing, value, nameof(Progressing));
        }
        private bool _Progressing;

        public object ChildSource => ChildCollection.Source;

        public int Depth { get; }

        public string DepthString =>
            _DepthString ?? (
            _DepthString = string.Create(Depth, default(object), (span, _) => span.Fill('>')));
        private string _DepthString;

        public OperationInfo Info { get; }

        public bool CompletedWithError {
            get => _CompletedWithError;
            private set => Change(ref _CompletedWithError, value, nameof(CompletedWithError));
        }
        private bool _CompletedWithError;

        public OperationBase(OperationInfo info) : this(info, null) {
        }

        public IOperationProgress Begin(CancellationToken cancellationToken) {
            Stopwatch = Stopwatch.StartNew();
            Progressing = true;
            return new ProgressWrapper(this, cancellationToken);
        }

        public void End() {
            Stopwatch.Stop();
            Progressing = false;
            ProgressPercent = Target == 0 ? 100 : ProgressPercent;
            OnCompleted(EventArgs.Empty);
        }

        //public async Task Operate(Func<IOperationProgress, CancellationToken, Task> task, CancellationToken cancellationToken) {
        //    if (null == task) throw new ArgumentNullException(nameof(task));
        //    if (Log.Info()) {
        //        Log.Info(nameof(Operate));
        //    }
        //    Progressing = true;
        //    Stopwatch = Stopwatch.StartNew();
        //    try {
        //        await task(new ProgressWrapper(this), cancellationToken);
        //    }
        //    catch (OperationCanceledException ex) {
        //        if (Log.Info()) {
        //            Log.Info(ex?.GetType()?.Name ?? nameof(OperationCanceledException));
        //        }
        //    }
        //    catch (Exception ex) {
        //        if (Log.Warn()) {
        //            Log.Warn(ex);
        //        }
        //        Error = ex;
        //    }
        //    Stopwatch.Stop();
        //    Progressing = false;

        //    if (Error != null) {
        //        CompletedWithError = true;
        //    }
        //    if (ChildCollection.Any(child => child.CompletedWithError)) {
        //        CompletedWithError = true;
        //    }
        //    if (CompletedWithError) {
        //        Relevant = true;
        //    }
        //    else {
        //        ProgressPercent = Target == 0 ? 100 : ProgressPercent;
        //    }

        //    OnCompleted(EventArgs.Empty);
        //}

        //public virtual void Cancel() {
        //    if (Log.Info()) {
        //        Log.Info(nameof(Cancel));
        //    }
        //    Canceling = true;
        //    CancellationTokenSource?.Cancel();
        //    foreach (var child in ChildCollection) {
        //        child.Cancel();
        //    }
        //}

        private class ProgressWrapper : IOperationProgress {
            public OperationBase Operation { get; }
            public CancellationToken CancellationToken { get; }

            public ProgressWrapper(OperationBase operation, CancellationToken cancellationToken) {
                Operation = operation ?? throw new ArgumentNullException(nameof(operation));
                CancellationToken = cancellationToken;
            }

            public void Target(long value) {
                Operation.SetTarget(value);
            }

            public void Progress(long value) {
                Operation.SetProgress(value);
            }
        }
    }
}
