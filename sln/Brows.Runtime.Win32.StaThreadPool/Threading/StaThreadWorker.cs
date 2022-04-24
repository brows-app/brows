using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Brows.Threading {
    using Logger;

    internal class StaThreadWorker {
        private Thread Thread;
        private Stopwatch Stopwatch;
        private ManualResetEventSlim StartEvent;
        private SynchronizationContext Context;

        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(StaThreadWorker)));
        private ILog _Log;

        private void LogInfo(string s) {
            if (Log.Info()) {
                Log.Info(
                    s,
                    $"{nameof(Pool)} > {Pool}",
                    $"{nameof(ID)} > {ID}");
            }
        }

        private void Application_Idle(object sender, EventArgs e) {
            LogInfo(nameof(Application_Idle));
            try {
                Application.Idle -= Application_Idle;
                Context = SynchronizationContext.Current;
                StartEvent.Set();
            }
            catch (Exception ex) {
                if (Log.Critical()) {
                    Log.Critical(ex);
                }
            }
        }

        private void ThreadStart() {
            LogInfo(nameof(ThreadStart));
            try {
                Application.Idle += Application_Idle;
                Application.Run();
            }
            catch (Exception ex) {
                if (Log.Critical()) {
                    Log.Critical(ex);
                }
            }
        }

        private async Task Start() {
            LogInfo(nameof(Start));
            await Task.Run(() => {
                using (StartEvent = new ManualResetEventSlim()) {
                    Thread = new Thread(ThreadStart);
                    Thread.IsBackground = true;
                    Thread.Name = $"{nameof(StaThreadWorker)} {Pool} {ID}";
                    Thread.SetApartmentState(ApartmentState.STA);
                    Thread.Start();
                    StartEvent.Wait();
                }
            });
        }

        public TimeSpan? IdleTime {
            get {
                var stopwatch = Stopwatch;
                return stopwatch == null
                    ? null
                    : stopwatch.Elapsed;
            }
        }

        public bool Working { get; set; }
        public long ID { get; }
        public string Pool { get; }

        public StaThreadWorker(string pool, long id) {
            ID = id;
            Pool = pool;
        }

        public void Exit() {
            LogInfo(nameof(Exit));
            Context.Post(state: null, d: _ => {
                try {
                    Application.ExitThread();
                }
                catch (Exception ex) {
                    if (Log.Critical()) {
                        Log.Critical(ex);
                    }
                }
            });
        }

        public async Task<TResult> Work<TResult>(StaThreadWorkItem<TResult> item, CancellationToken cancellationToken) {
            LogInfo(nameof(Work));
            Stopwatch = null;
            if (Context == null) {
                await Start();
            }
            var taskSource = new TaskCompletionSource<TResult>();
            if (cancellationToken.IsCancellationRequested) {
                taskSource.SetCanceled(cancellationToken);
                return await taskSource.Task;
            }
            Context.Post(state: null, d: _ => {
                try {
                    cancellationToken.ThrowIfCancellationRequested();
                    taskSource.SetResult(item == null
                        ? default
                        : item.Invoke());
                }
                catch (Exception ex) {
                    if (ex is OperationCanceledException canceled && canceled.CancellationToken.Equals(cancellationToken)) {
                        taskSource.SetCanceled(cancellationToken);
                    }
                    else {
                        taskSource.SetException(ex);
                    }
                }
            });
            try {
                return await taskSource.Task;
            }
            finally {
                Stopwatch = Stopwatch.StartNew();
            }
        }
    }
}
