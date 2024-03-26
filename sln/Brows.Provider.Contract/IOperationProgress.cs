using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows {
    public interface IOperationProgress {
        OperationProgressKind Kind { get; }
        void Change(long? addProgress = null, long? setProgress = null, long? addTarget = null, long? setTarget = null, string name = null, string data = null, OperationProgressKind? kind = null);
        Task Child(string name, OperationProgressKind kind, OperationDelegate task);

        public async Task<bool> Children<T>(IEnumerable<T> source, Func<T, (string name, OperationProgressKind kind, OperationDelegate task)> child) {
            ArgumentNullException.ThrowIfNull(source);
            var tasks = source
                .Select(item => {
                    var (name, kind, task) = child(item);
                    return Child(name, kind, task);
                })
                .ToList();
            if (tasks.Count == 0) {
                return false;
            }
            await Task.WhenAll(tasks);
            return true;
        }
    }
}
