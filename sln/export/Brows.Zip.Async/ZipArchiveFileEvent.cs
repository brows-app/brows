using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows {
    public sealed class ZipArchiveFileEvent {
        private readonly List<Func<Task>> Tasks = [];

        internal void Clear() {
            lock (Tasks) {
                Tasks.Clear();
            }
        }

        public void Add(Func<Task> task) {
            ArgumentNullException.ThrowIfNull(task);
            lock (Tasks) {
                Tasks.Add(task);
            }
        }

        public bool Remove(Func<Task> task) {
            lock (Tasks) {
                return Tasks.Remove(task);
            }
        }

        public async Task All() {
            var tasks = new List<Func<Task>>();
            lock (Tasks) {
                tasks.AddRange(Tasks);
            }
            await Task.WhenAll(tasks.Select(task => task()));
        }
    }
}
