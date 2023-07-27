using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class CommandProcessHistory {
        private readonly HashSet<string> Items = new HashSet<string>();

        public async IAsyncEnumerable<string> Get([EnumeratorCancellation] CancellationToken cancellationToken) {
            foreach (var item in Items) {
                yield return item;
            }
            await Task.CompletedTask;
        }

        public async Task Add(string input, CancellationToken cancellationToken) {
            Items.Add(input);
            await Task.CompletedTask;
        }
    }
}
