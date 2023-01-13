using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.Threading {
    using Logs;

    internal class STAThreadWorker {
        private static readonly ILog Log = Logging.For(typeof(STAThreadWorker));

        private Stopwatch Stopwatch;

        private STAThreadContext Context =>
            _Context ?? (
            _Context = new STAThreadContext($"{nameof(STAThreadWorker)} {Pool}.{ID:00}"));
        private STAThreadContext _Context;

        public TimeSpan? IdleTime =>
            Stopwatch?.Elapsed;

        public bool Working { get; set; }
        public long ID { get; }
        public string Pool { get; }

        public STAThreadWorker(string pool, long id) {
            ID = id;
            Pool = pool;
        }

        public void Exit() {
            if (Log.Info()) {
                Log.Info(this + " " + nameof(Exit) + " [" + IdleTime + "]");
            }
            Context.Exit();
        }

        public async Task<TResult> Work<TResult>(STAThreadWorkItem<TResult> item, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(this + " [" + item?.Name + "]");
            }
            Stopwatch = null;
            try {
                if (cancellationToken.IsCancellationRequested) {
                    return await Task.FromCanceled<TResult>(cancellationToken);
                }
                var taskSource = new TaskCompletionSource<TResult>();
                var
                context = await Context.Ready();
                context.Post(state: null, d: async _ => {
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
                return await taskSource.Task;
            }
            finally {
                Stopwatch = Stopwatch.StartNew();
            }
        }

        public override string ToString() {
            return Pool + "." + ID.ToString("00") + (Working ? " [work]" : " [idle]");
        }
    }
}
