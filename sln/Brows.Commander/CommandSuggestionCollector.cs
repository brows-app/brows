using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class CommandSuggestionCollector {
        private static readonly ILog Log = Logging.For(typeof(CommandSuggestionCollector));

        private static T Catch<T>(Func<T> f) {
            try {
                return f();
            }
            catch (Exception ex) {
                if (Log.Warn()) {
                    Log.Warn(ex);
                }
                return default;
            }
        }

        private static async Task<T> Catch<T>(Task<T> t, CancellationToken token) {
            if (t is null) {
                return default;
            }
            try {
                return await t.ConfigureAwait(false);
            }
            catch (Exception ex) {
                if (ex is OperationCanceledException canceled && canceled.CancellationToken == token) {
                    throw;
                }
                if (Log.Warn()) {
                    Log.Warn(ex);
                }
                return default;
            }
        }

        private static Task Catch(Task t, CancellationToken token) {
            async Task<object> task() {
                await t.ConfigureAwait(false);
                return default;
            }
            return Catch(task(), token);
        }

        private async IAsyncEnumerable<ICommandSuggestion> Suggest([EnumeratorCancellation] CancellationToken token) {
            var commands = Commands ?? throw new InvalidOperationException();
            var disposed = new List<Task>();
            var suggestions = commands
                .Select(c => Catch(() => c.Suggest(Context, token)))
                .Where(s => s != default);
            var enumerators = suggestions
                .Select(s => Catch(() => s.GetAsyncEnumerator(token)))
                .Where(e => e != default)
                .ToList();
            var tasks = enumerators
                .Select(e => Catch(() => e.MoveNextAsync().AsTask()))
                .Select(t => Catch(t, token))
                .ToList();
            try {
                for (; ; ) {
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    if (tasks.Count == 0) break;
                    var task = await Task.WhenAny(tasks).ConfigureAwait(false);
                    var index = tasks.IndexOf(task);
                    var enumerator = enumerators[index];
                    var result = await task.ConfigureAwait(false);
                    if (result) {
                        yield return enumerator.Current;
                        tasks[index] = Catch(Catch(() => enumerator.MoveNextAsync().AsTask()), token);
                    }
                    else {
                        tasks.RemoveAt(index);
                        enumerators.RemoveAt(index);
                        disposed.Add(Catch(Catch(() => enumerator.DisposeAsync().AsTask()), token));
                    }
                }
            }
            finally {
                disposed.AddRange(enumerators.Select(e => Catch(Catch(() => e.DisposeAsync().AsTask()), token)));
                await Task.WhenAll(disposed).ConfigureAwait(false);
            }
        }

        public event EventHandler InputSuggestionChanged;

        public ICommandContext Context { get; set; }
        public IEnumerable<ICommand> Commands { get; set; }
        public CommandSuggestionCollection Collection { get; set; }
        public int InputSuggestionRelevance { get; private set; } = int.MinValue;
        public string InputSuggestion { get; private set; }

        public async Task Collect(CancellationToken token) {
            var context = Context ?? throw new InvalidOperationException();
            var collection = Collection ?? throw new InvalidOperationException();
            collection.Clear();
            InputSuggestionRelevance = int.MinValue;
            InputSuggestion = null;
            InputSuggestionChanged?.Invoke(this, EventArgs.Empty);
            await foreach (var suggestion in Suggest(token).ConfigureAwait(false)) {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                else {
                    collection.Add(suggestion);
                }
                if (context.HasInput(out var input)) {
                    var suggInp = suggestion.Input;
                    if (suggInp.StartsWith(input, StringComparison.CurrentCultureIgnoreCase)) {
                        var relevance = suggestion.Relevance;
                        if (relevance > InputSuggestionRelevance) {
                            InputSuggestionRelevance = relevance;
                            InputSuggestion = suggInp;
                            InputSuggestionChanged?.Invoke(this, EventArgs.Empty);
                        }
                    }
                }
            }
        }
    }
}
