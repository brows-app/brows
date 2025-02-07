using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.Threading {
    public sealed class STAThreadPool {
        private static readonly ILog Log = Logging.For(typeof(STAThreadPool));

        private long WorkerID;
        private Timer Timer;
        private TimeSpan TimerPeriod = TimeSpan.FromMinutes(1);
        private readonly object TimerLock = new();
        private readonly List<STAThreadWorker> Workers = new();

        private void TimerStart() {
            lock (TimerLock) {
                Timer?.Change(Timeout.Infinite, Timeout.Infinite);
                Timer?.Dispose();
                Timer = new Timer(TimerCallback, null, TimerPeriod, Timeout.InfiniteTimeSpan);
            }
        }

        private void TimerCallback(object _) {
            if (Log.Info()) {
                Log.Info(nameof(TimerCallback));
            }
            lock (TimerLock) {
                Timer?.Change(Timeout.Infinite, Timeout.Infinite);
                Timer?.Dispose();
                Timer = null;
            }
            lock (Workers) {
                if (Workers.Count <= WorkerCountMin) {
                    return;
                }
                var idle = default(List<STAThreadWorker>);
                foreach (var worker in Workers) {
                    if (worker.Working == false) {
                        var idleTime = worker.IdleTime;
                        if (idleTime.HasValue) {
                            if (idleTime.Value > IdleTime) {
                                idle = idle ?? new List<STAThreadWorker>();
                                idle.Add(worker);
                            }
                        }
                    }
                }
                if (idle != null) {
                    foreach (var worker in idle) {
                        worker.Exit();
                        Workers.Remove(worker);
                    }
                }
                if (Workers.Count > WorkerCountMin) {
                    TimerStart();
                }
            }
        }

        private async Task<(bool worked, TResult result)> TryWork<TResult>(STAThreadWorkItem<TResult> item, CancellationToken cancellationToken) {
            var worker = default(STAThreadWorker);
            lock (Workers) {
                var idle = Workers.FirstOrDefault(w => w.Working == false);
                if (idle == null && WorkerCountMax > Workers.Count) {
                    idle = new STAThreadWorker(Name, ++WorkerID);
                    Workers.Add(idle);
                }
                if (idle == null) {
                    return (worked: false, result: default);
                }
                worker = idle;
                worker.Working = true;
            }
            try {
                return (worked: true, result: await worker.Work(item, cancellationToken));
            }
            finally {
                lock (Workers) {
                    worker.Working = false;
                    if (Workers.Count > WorkerCountMin) {
                        TimerStart();
                    }
                }
            }
        }

        private async Task<TResult> DoWork<TResult>(STAThreadWorkItem<TResult> item, CancellationToken cancellationToken) {
            for (; ; ) {
                var tryWork = await TryWork(item, cancellationToken);
                if (tryWork.worked) {
                    return tryWork.result;
                }
                await Task.Delay(TryWorkDelay, cancellationToken);
            }
        }

        private async Task<TResult> Work<TResult>(STAThreadWorkItem<TResult> item, CancellationToken cancellationToken) {
            return await DoWork(item, cancellationToken);
        }

        public TimeSpan IdleTime { get; set; } = TimeSpan.FromMinutes(2.5);
        public int TryWorkDelay { get; set; } = 10;
        public int WorkerCountMax { get; set; } = 16;
        public int WorkerCountMin { get; set; } = 1;

        public string Name { get; }

        public STAThreadPool(string name) {
            Name = name;
        }

        public Task<TResult> Work<TResult>(string name, Func<TResult> work, CancellationToken cancellationToken) {
            return Work(new STAThreadWorkItem<TResult>(name, work), cancellationToken);
        }

        public Task<TResult> Work<TResult>(string name, Func<CancellationToken, Task<TResult>> work, CancellationToken cancellationToken) {
            return Work(new STAThreadWorkItem<TResult>(name, work), cancellationToken);
        }

        public async Task Work(string name, Action work, CancellationToken cancellationToken) {
            await Work<object>(name, () => {
                if (work != null) {
                    work();
                }
                return default;
            }, cancellationToken);
        }

        public async Task Work(string name, Func<CancellationToken, Task> work, CancellationToken cancellationToken) {
            await Work<object>(name, async token => {
                if (work != null) {
                    await work(token);
                }
                return default;
            }, cancellationToken);
        }

        public void Empty() {
            if (Log.Info()) {
                Log.Info(nameof(Empty));
            }
            lock (Workers) {
                var workers = new List<STAThreadWorker>(Workers);
                foreach (var worker in workers) {
                    worker.Exit();
                    Workers.Remove(worker);
                }
            }
        }
    }
}
