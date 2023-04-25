using Domore.Notification;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Collections.ObjectModel {
    public class ObservableSource<T> : Notifier {
        private static readonly PropertyChangedEventArgs CountEvent = new(nameof(Count));
        private static readonly PropertyChangedEventArgs OptionsEvent = new(nameof(Options));
        private static readonly PropertyChangedEventArgs SourceEvent = new(nameof(Source));
        private static readonly PropertyChangedEventArgs CollectionEvent = new(nameof(Collection));

        private readonly object CollectingLocker = new();

        private int DelayOpportunityCount;
        private int DelayOpportunityTicks;

        private ObservableCollection<T> Collection {
            get => _Collection ?? (_Collection = new());
            set => Change(ref _Collection, value, CollectionEvent, SourceEvent);
        }
        private ObservableCollection<T> _Collection;

        private readonly ConcurrentQueue<QueueWork> Queue = new();

        private async Task DelayOpportunity(CancellationToken token) {
            if (DelayOpportunityCount++ > Options.DelayAfterCount) {
                var delay = Options.Delay;
                if (delay > 0) {
                    var now = Environment.TickCount;
                    var dif = now - DelayOpportunityTicks;
                    if (dif < delay) {
                        var d = delay - dif;
                        if (d > 0) {
                            await Task.Delay(d, token);
                            DelayOpportunityTicks = Environment.TickCount;
                        }
                    }
                }
                DelayOpportunityCount = 0;
            }
        }

        public object Source =>
            Collection;

        public ObservableSourceOptions Options {
            get => _Options ?? (_Options = new());
            set => Change(ref _Options, value, OptionsEvent);
        }
        private ObservableSourceOptions _Options;

        public int Count {
            get => _Count;
            private set => Change(ref _Count, value, CountEvent);
        }
        private int _Count;

        public async Task Add(IReadOnlyCollection<T> collection, CancellationToken token) {
            if (collection is null) throw new ArgumentNullException(nameof(collection));
            var count = collection.Count;
            if (count > 0) {
                var work = new QueueWork(this, QueueWorkKind.Add, collection, token);
                Queue.Enqueue(work);
                await work.Task;
            }
        }

        public async Task Add(T item, CancellationToken token) {
            await Add(new[] { item }, token);
        }

        public async Task Remove(IReadOnlyCollection<T> collection, CancellationToken token) {
            if (collection is null) throw new ArgumentNullException(nameof(collection));
            var count = collection.Count;
            if (count == 0) {
                return;
            }
            var work = new QueueWork(this, QueueWorkKind.Remove, collection, token);
            Queue.Enqueue(work);
            await work.Task;
        }

        public async Task Remove(T item, CancellationToken token) {
            await Remove(new[] { item }, token);
        }

        public async Task Clear(CancellationToken token) {
            var work = new QueueWork(this, QueueWorkKind.Clear, Array.Empty<T>(), token);
            Queue.Enqueue(work);
            await work.Task;
        }

        public async Task Collect(CancellationToken token) {
            using (var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Options.CollectionPeriod))) {
                for (; ; ) {
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    var dequeued = Queue.TryDequeue(out var work);
                    if (dequeued) {
                        await work.Done(token);
                    }
                    else {
                        await timer.WaitForNextTickAsync(token);
                    }
                }
            }
        }

        public async void BeginCollect(CancellationToken token) {
            try {
                await Collect(token);
            }
            catch (OperationCanceledException canceled) when (canceled.CancellationToken == token) {
            }
        }

        private enum QueueWorkKind {
            Add,
            Remove,
            Clear
        }

        private sealed class QueueWork {
            private readonly TaskCompletionSource Complete = new();

            public Task Task => Complete.Task;

            public QueueWorkKind Kind { get; }
            public IReadOnlyCollection<T> Items { get; }
            public CancellationToken Token { get; }
            public ObservableSource<T> Source { get; }

            public QueueWork(ObservableSource<T> source, QueueWorkKind kind, IReadOnlyCollection<T> items, CancellationToken token) {
                Kind = kind;
                Token = token;
                Items = items ?? throw new ArgumentNullException(nameof(items));
                Source = source ?? throw new ArgumentNullException(nameof(source));
            }

            public async Task Done(CancellationToken token) {
                async Task work(CancellationToken token) {
                    var kind = Kind;
                    if (kind == QueueWorkKind.Clear) {
                        Source.Collection.Clear();
                        Source.Count = Source.Collection.Count;
                        return;
                    }
                    if (Source.Collection.Count == 0 && kind == QueueWorkKind.Add) {
                        Source.Collection = new(Items);
                        Source.Count = Source.Collection.Count;
                        return;
                    }
                    foreach (var item in Items) {
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        switch (kind) {
                            case QueueWorkKind.Add:
                                Source.Collection.Add(item);
                                goto default;
                            case QueueWorkKind.Remove:
                                Source.Collection.Remove(item);
                                goto default;
                            default:
                                Source.Count = Source.Collection.Count;
                                break;
                        }
                        await Source.DelayOpportunity(token);
                    }
                }
                var error = default(Exception);
                try {
                    using (var linked = CancellationTokenSource.CreateLinkedTokenSource(Token, token)) {
                        await work(linked.Token);
                    }
                }
                catch (Exception ex) {
                    error = ex;
                }
                if (error != null) {
                    if (error is OperationCanceledException canceled && (canceled.CancellationToken == Token || canceled.CancellationToken == token)) {
                        Complete.SetCanceled(canceled.CancellationToken);
                    }
                    else {
                        Complete.SetException(error);
                    }
                }
                else {
                    Complete.SetResult();
                }
            }
        }

        private sealed class CollectingException : InvalidOperationException {
        }
    }
}
