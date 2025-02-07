using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public sealed class ZipArchiveFileEvent {
        private readonly List<Func<CancellationToken, Task>> Tasks = [];

        internal void Clear() {
            lock (Tasks) {
                Tasks.Clear();
            }
        }

        public void Add(Func<CancellationToken, Task> task) {
            ArgumentNullException.ThrowIfNull(task);
            lock (Tasks) {
                Tasks.Add(task);
            }
        }

        public bool Remove(Func<CancellationToken, Task> task) {
            lock (Tasks) {
                return Tasks.Remove(task);
            }
        }

        public Task All(CancellationToken token) {
            var tasks = new List<Func<CancellationToken, Task>>();
            lock (Tasks) {
                tasks.AddRange(Tasks);
            }
            return Task.WhenAll(tasks.Select(task => task(token)));
        }
    }
}
