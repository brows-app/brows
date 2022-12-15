using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal class CommandSuggester {
        public ICommandContext Context { get; }
        public IEnumerable<ICommand> Commands { get; }

        public CommandSuggester(ICommandContext context, IEnumerable<ICommand> commands) {
            Context = context;
            Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public async IAsyncEnumerable<ICommandSuggestion> Suggest([EnumeratorCancellation] CancellationToken cancellationToken) {
            var dispose = new List<Task>();
            var commands = Commands;
            var suggestions = commands.Select(c => c.Suggest(Context, cancellationToken));
            var enumerators = suggestions.Select(s => s.GetAsyncEnumerator()).ToList();
            var tasks = enumerators.Select(e => e.MoveNextAsync().AsTask()).ToList();
            try {
                for (; ; ) {
                    if (tasks.Count == 0) break;
                    var task = await Task.WhenAny(tasks);
                    var index = tasks.IndexOf(task);
                    var enumerator = enumerators[index];

                    var result = await task;
                    if (result) {
                        yield return enumerator.Current;
                        tasks[index] = enumerator.MoveNextAsync().AsTask();
                    }
                    else {
                        tasks.RemoveAt(index);
                        enumerators.RemoveAt(index);
                        dispose.Add(enumerator.DisposeAsync().AsTask());
                    }
                }
            }
            finally {
                await Task.WhenAll(dispose
                    .Concat(enumerators
                        .Select(e => e.DisposeAsync().AsTask())));
            }
        }
    }
}
