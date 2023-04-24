using Domore.Logs;
using Domore.Notification;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal class OperationBase : Notifier, IOperation, IOperable {
        private static readonly ILog Log = Logging.For(typeof(OperationBase));
        private static readonly PropertyChangedEventArgs NameEvent = new(nameof(Name));
        private static readonly PropertyChangedEventArgs DataEvent = new(nameof(Data));
        private static readonly PropertyChangedEventArgs ProgressingEvent = new(nameof(Progressing));
        private static readonly PropertyChangedEventArgs CancelingEvent = new(nameof(Canceling));
        private static readonly PropertyChangedEventArgs CompletedWithErrorEvent = new(nameof(CompletedWithError));
        private static readonly PropertyChangedEventArgs RelevantEvent = new(nameof(Relevant));
        private static readonly PropertyChangedEventArgs ProgressPercentEvent = new(nameof(ProgressPercent));
        private static readonly PropertyChangedEventArgs ErrorMessageEvent = new(nameof(ErrorMessage));
        private static readonly PropertyChangedEventArgs ErrorEvent = new(nameof(Error));
        private static readonly PropertyChangedEventArgs TargetEvent = new(nameof(Target));
        private static readonly PropertyChangedEventArgs ProgressEvent = new(nameof(Progress));
        private static readonly PropertyChangedEventArgs[] TargetDependents = new[] { ProgressPercentEvent };
        private static readonly PropertyChangedEventArgs[] ProgressDependents = new[] { ProgressPercentEvent };
        private static readonly PropertyChangedEventArgs[] ErrorDependents = new[] { ErrorMessageEvent };

        private bool MakingRelevant;
        private Stopwatch Stopwatch;
        private CancellationTokenSource TokenSource;
        private readonly OperationBaseCollection ChildCollection = new();
        private readonly object TokenLocker = new();
        private readonly object ProgressLocker = new();
        private readonly object RelevantLocker = new();

        private OperationBase Child(string name) {
            if (Log.Info()) {
                Log.Info(nameof(Child) + " > " + name);
            }
            var child = new OperationBase(name, this);
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
                await Task.Delay(1000, token);
            }
            catch (Exception ex) {
                if (ex is OperationCanceledException canceled && canceled.CancellationToken == token) {
                    return;
                }
                else {
                    throw;
                }
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

        private double CalcProgressPercent() {
            return Target == 0
                ? 0
                : (double)Progress / Target * 100;
        }

        private void SetTarget(long value) {
            if (Log.Debug()) {
                Log.Debug(nameof(SetTarget) + " > " + value);
            }
            lock (ProgressLocker) {
                if (Parent != null) {
                    Parent.SetTarget(Parent.Target + (value - Target));
                }
                Target = value;
                ProgressPercent = CalcProgressPercent();
            }
            NotifyTargetChanged();
        }

        private void AddTarget(long value) {
            if (Log.Debug()) {
                Log.Debug(nameof(AddTarget) + " > " + value);
            }
            lock (ProgressLocker) {
                if (Parent != null) {
                    Parent.AddTarget(value);
                }
                Target += value;
                ProgressPercent = CalcProgressPercent();
            }
            NotifyTargetChanged();
        }

        private void SetProgress(long value) {
            if (Log.Debug()) {
                Log.Debug(nameof(SetProgress) + " > " + value);
            }
            lock (ProgressLocker) {
                if (Parent != null) {
                    Parent.SetProgress(Parent.Progress + (value - Progress));
                }
                Progress = value;
                ProgressPercent = CalcProgressPercent();
            }
            NotifyProgressChanged();
        }

        private void AddProgress(long value) {
            if (Log.Debug()) {
                Log.Debug(nameof(AddProgress) + " > " + value);
            }
            lock (ProgressLocker) {
                if (Parent != null) {
                    Parent.AddProgress(value);
                }
                Progress += value;
                ProgressPercent = CalcProgressPercent();
            }
            NotifyProgressChanged();
        }

        protected virtual void Cancel() {
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

        public string ErrorMessage =>
            _Error?.Message;

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

        public bool CompletedWithError {
            get => _CompletedWithError;
            private set => Change(ref _CompletedWithError, value, CompletedWithErrorEvent);
        }
        private bool _CompletedWithError;

        public OperationBase Parent { get; }

        public OperationBase(string name, OperationBase parent = null) {
            Name = name;
            Parent = parent;
            Depth = Parent == null ? 0 : (Parent.Depth + 1);
        }

        public async void Operate(Func<IOperationProgress, CancellationToken, Task> task) {
            if (null == task) throw new ArgumentNullException(nameof(task));
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
                    await task(progress, token);
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
                CompletedWithError = true;
            }
            if (ChildCollection.Any(child => child.CompletedWithError)) {
                CompletedWithError = true;
            }
            if (CompletedWithError) {
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
            Completed?.Invoke(this, EventArgs.Empty);
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

            public ProgressWrapper(OperationBase operation) {
                Operation = operation ?? throw new ArgumentNullException(nameof(operation));
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
