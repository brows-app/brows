using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Threading {
    using Logger;

    public class StaThreadPool {
        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(StaThreadPool)));
        private ILog _Log;

        private long WorkerID;
        private Timer Timer;
        private TimeSpan TimerPeriod = TimeSpan.FromMinutes(1);
        private readonly List<StaThreadWorker> Workers = new List<StaThreadWorker>();

        private void TimerStart() {
            Timer?.Change(Timeout.Infinite, Timeout.Infinite);
            Timer?.Dispose();
            Timer = new Timer(TimerCallback, null, TimerPeriod, Timeout.InfiniteTimeSpan);
        }

        private void TimerCallback(object _) {
            if (Log.Info()) {
                Log.Info(nameof(TimerCallback));
            }
            lock (Workers) {
                if (Workers.Count < 2) {
                    return;
                }
                var idle = default(List<StaThreadWorker>);
                foreach (var worker in Workers) {
                    if (worker.Working == false) {
                        var idleTime = worker.IdleTime;
                        if (idleTime.HasValue) {
                            if (Log.Info()) {
                                Log.Info(
                                    $"{nameof(worker.Pool)} > {worker.Pool}",
                                    $"{nameof(worker.ID)} > {worker.ID}",
                                    $"{nameof(worker.IdleTime)} > {worker.IdleTime}");
                            }
                            if (idleTime.Value > IdleTime) {
                                idle = idle ?? new List<StaThreadWorker>();
                                idle.Add(worker);
                            }
                        }
                    }
                }
                if (idle != null) {
                    foreach (var worker in idle) {
                        worker.Exit();
                        Workers.Remove(worker);
                        if (Log.Info()) {
                            Log.Info(
                                nameof(Workers.Remove),
                                $"{nameof(worker.Pool)} > {worker.Pool}",
                                $"{nameof(worker.ID)} > {worker.ID}");
                        }
                    }
                }
                if (Workers.Count > 1) {
                    TimerStart();
                }
            }
        }

        public TimeSpan IdleTime { get; set; } = TimeSpan.FromMinutes(5);

        public string Name { get; }

        public StaThreadPool(string name) {
            Name = name;
        }

        public async Task<TResult> Work<TResult>(Func<TResult> work, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(Work));
            }
            var item = new StaThreadWorkItem<TResult>(work);
            var worker = default(StaThreadWorker);
            lock (Workers) {
                var idle = Workers.FirstOrDefault(w => w.Working == false);
                if (idle == null) {
                    idle = new StaThreadWorker(Name, ++WorkerID);
                    Workers.Add(idle);
                    if (Log.Info()) {
                        Log.Info(
                            nameof(Workers.Add),
                            $"{nameof(idle.Pool)} > {idle.Pool}",
                            $"{nameof(idle.ID)} > {idle.ID}");
                    }
                }
                idle.Working = true;
                worker = idle;
            }
            try {
                return await worker.Work(item, cancellationToken);
            }
            finally {
                lock (Workers) {
                    worker.Working = false;
                    if (Workers.Count > 1) {
                        TimerStart();
                    }
                }
            }
        }

        public async Task Work(Action work, CancellationToken cancellationToken) {
            await Work<object>(() => {
                work?.Invoke();
                return default;
            }, cancellationToken);
        }
    }
}
