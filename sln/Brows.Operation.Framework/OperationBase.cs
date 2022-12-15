using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using ComponentModel;
    using Logger;

    internal class OperationBase : NotifyPropertyChanged, IOperation, IOperable {
        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(OperationBase)));
        private ILog _Log;

        private bool MakingRelevant;
        private Stopwatch Stopwatch;
        private CancellationTokenSource CancellationTokenSource;
        private readonly OperationCollection ChildCollection;

        private OperationBase Child(string name) {
            if (Log.Info()) {
                Log.Info(nameof(Child) + " > " + name);
            }
            var child = new OperationBase(name, this);
            ChildCollection.Add(child);
            return child;
        }

        private void MakeRelevant() {
            if (Log.Debug()) {
                Log.Debug(nameof(MakeRelevant));
            }
            if (Relevant == false) {
                if (MakingRelevant == false) {
                    MakingRelevant = true;
                    Task.Delay(1000).ContinueWith(_ => {
                        if (Progressing) {
                            Relevant = true;
                        }
                        MakingRelevant = false;
                    });
                }
            }
        }

        private void SetTarget(long value) {
            if (Log.Debug()) {
                Log.Debug(nameof(SetTarget) + " > " + value);
            }
            if (Parent != null) {
                Parent.SetTarget(Parent.Target + (value - Target));
            }
            Target = value;
            OnTargetChanged(EventArgs.Empty);
            MakeRelevant();
        }

        private void AddTarget(long value) {
            if (Log.Debug()) {
                Log.Debug(nameof(AddTarget) + " > " + value);
            }
            if (Parent != null) {
                Parent.AddTarget(value);
            }
            Target += value;
            OnTargetChanged(EventArgs.Empty);
            MakeRelevant();
        }

        private void SetProgress(long value) {
            if (Log.Debug()) {
                Log.Debug(nameof(SetProgress) + " > " + value);
            }
            if (Parent != null) {
                Parent.SetProgress(Parent.Progress + (value - Progress));
            }
            Progress = value;
            OnProgressChanged(EventArgs.Empty);
            MakeRelevant();
        }

        private void AddProgress(long value) {
            if (Log.Debug()) {
                Log.Debug(nameof(AddProgress) + " > " + value);
            }
            if (Parent != null) {
                Parent.AddProgress(value);
            }
            Progress += value;
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

        protected virtual void Cancel() {
            if (Log.Info()) {
                Log.Info(nameof(Cancel) + " > " + Name);
            }
            Canceling = true;
            foreach (var child in ChildCollection) {
                child.Cancel();
            }
            CancellationTokenSource?.Cancel();
        }

        public event EventHandler RelevantChanged;
        public event EventHandler TargetChanged;
        public event EventHandler ProgressChanged;
        public event EventHandler Completed;

        public string Name {
            get => _Name;
            private set => Change(ref _Name, value, nameof(Name));
        }
        private string _Name;

        public string Data {
            get => _Data;
            private set => Change(ref _Data, value, nameof(Data));
        }
        private string _Data;

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

        public bool CompletedWithError {
            get => _CompletedWithError;
            private set => Change(ref _CompletedWithError, value, nameof(CompletedWithError));
        }
        private bool _CompletedWithError;

        public OperationBase Parent { get; }

        public OperationBase(string name, OperationBase parent = null) {
            Name = name;
            Parent = parent;
            Depth = Parent == null ? 0 : (Parent.Depth + 1);
            ChildCollection = new OperationCollection();
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

        public virtual async Task Operate(Func<IOperationProgress, CancellationToken, Task> task, CancellationToken? cancellationToken) {
            if (null == task) throw new ArgumentNullException(nameof(task));
            if (Log.Info()) {
                Log.Info(nameof(Operate));
            }
            var token = cancellationToken.HasValue
                ? cancellationToken.Value
                : (CancellationTokenSource = new CancellationTokenSource()).Token;
            using (CancellationTokenSource) {
                Progressing = true;
                Stopwatch = Stopwatch.StartNew();
                try {
                    await task(new ProgressWrapper(this, token), token);
                }
                catch (OperationCanceledException ex) {
                    if (ex?.CancellationToken != token) {
                        throw;
                    }
                    if (Log.Info()) {
                        Log.Info(ex?.GetType()?.Name ?? nameof(OperationCanceledException));
                    }
                }
                catch (Exception ex) {
                    if (Log.Warn()) {
                        Log.Warn(ex);
                    }
                    Error = ex;
                }
                Stopwatch.Stop();
                Progressing = false;
            }
            if (Error != null) {
                CompletedWithError = true;
            }
            if (ChildCollection.Any(child => child.CompletedWithError)) {
                CompletedWithError = true;
            }
            if (CompletedWithError) {
                Relevant = true;
            }
            else {
                ProgressPercent = Target == 0 ? 100 : ProgressPercent;
            }
            OnCompleted(EventArgs.Empty);
        }

        private class ProgressWrapper : IOperationProgress {
            public IOperationProgressInfo Info =>
                _Info ?? (
                _Info = new InfoWrapper(Operation));
            private IOperationProgressInfo _Info;

            public IOperationProgressTarget Target =>
                _Target ?? (
                _Target = new TargetWrapper(Operation));
            private IOperationProgressTarget _Target;

            public OperationBase Operation { get; }
            public CancellationToken CancellationToken { get; }

            public ProgressWrapper(OperationBase operation, CancellationToken cancellationToken) {
                Operation = operation ?? throw new ArgumentNullException(nameof(operation));
                CancellationToken = cancellationToken;
            }

            public long Get() {
                return Operation.Progress;
            }

            public void Add(long value) {
                Operation.AddProgress(value);
            }

            public void Set(long value) {
                Operation.SetProgress(value);
            }

            public IOperable Child(string name) {
                return Operation.Child(name);
            }

            private class InfoWrapper : IOperationProgressInfo {
                public OperationBase Operation { get; }

                public InfoWrapper(OperationBase operation) {
                    Operation = operation ?? throw new ArgumentNullException(nameof(operation));
                }

                public void Name(string value) {
                    Operation.Name = value;
                }

                public void Data(string format, params object[] args) {
                    // TODO: Translate this.
                    Operation.Data = string.Format(format, args);
                }
            }

            private class TargetWrapper : IOperationProgressTarget {
                public OperationBase Operation { get; }

                public TargetWrapper(OperationBase operation) {
                    Operation = operation ?? throw new ArgumentNullException(nameof(operation));
                }

                public long Get() {
                    return Operation.Target;
                }

                public void Add(long value) {
                    Operation.AddTarget(value);
                }

                public void Set(long value) {
                    Operation.SetTarget(value);
                }
            }
        }
    }
}
