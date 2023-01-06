using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Domore.Threading {
    using Logs;

    internal class STAThreadWorker {
        private static readonly ILog Log = Logging.For(typeof(STAThreadWorker));

        private Thread Thread;
        private Stopwatch Stopwatch;
        private ManualResetEventSlim StartEvent;
        private SynchronizationContext Context;

        private void Application_Idle(object sender, EventArgs e) {
            if (Log.Info()) {
                Log.Info(this, nameof(Application_Idle));
            }
            try {
                Application.Idle -= Application_Idle;
                Context = SynchronizationContext.Current;
                StartEvent.Set();
            }
            catch (Exception ex) {
                if (Log.Error()) {
                    Log.Error(this, ex);
                }
            }
        }

        private void ThreadStart() {
            if (Log.Info()) {
                Log.Info(this, nameof(ThreadStart));
            }
            try {
                Application.Idle += Application_Idle;
                Application.Run();
            }
            catch (Exception ex) {
                if (Log.Error()) {
                    Log.Error(this, ex);
                }
            }
        }

        private async Task Start() {
            if (Log.Info()) {
                Log.Info(this, nameof(Start));
            }
            await Task.Run(() => {
                using (StartEvent = new ManualResetEventSlim()) {
                    Thread = new Thread(ThreadStart);
                    Thread.IsBackground = true;
                    Thread.Name = $"{nameof(STAThreadWorker)} {Pool} {ID}";
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

        public STAThreadWorker(string pool, long id) {
            ID = id;
            Pool = pool;
        }

        public void Exit() {
            if (Log.Info()) {
                Log.Info(this, nameof(Exit));
            }
            Context.Post(state: null, d: _ => {
                try {
                    Application.ExitThread();
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(this, ex);
                    }
                }
            });
        }

        public async Task<TResult> Work<TResult>(STAThreadWorkItem<TResult> item, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(this, $"{nameof(Work)} [{item?.Name}]");
            }
            Stopwatch = null;
            if (Context == null) {
                await Start();
            }
            var taskSource = new TaskCompletionSource<TResult>();
            if (cancellationToken.IsCancellationRequested) {
                taskSource.SetCanceled(cancellationToken);
                return await taskSource.Task;
            }
            Context.Post(state: null, d: async _ => {
                try {
                    cancellationToken.ThrowIfCancellationRequested();
                    taskSource.SetResult(item == null
                        ? default
                        : await item.Invoke(cancellationToken));
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

        public override string ToString() {
            return Pool + "." + ID;
        }
    }
}
