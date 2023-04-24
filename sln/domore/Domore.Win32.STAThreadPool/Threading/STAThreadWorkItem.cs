using System;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.Threading {
    internal class STAThreadWorkItem<TResult> {
        public string Name { get; }
        public bool Async { get; }
        public Func<TResult> Function { get; }
        public Func<CancellationToken, Task<TResult>> FunctionAsync { get; }

        public STAThreadWorkItem(string name, Func<TResult> function) {
            Name = name;
            Async = false;
            Function = function;
        }

        public STAThreadWorkItem(string name, Func<CancellationToken, Task<TResult>> functionAsync) {
            Name = name;
            Async = true;
            FunctionAsync = functionAsync;
        }

        public async Task<TResult> Invoke(CancellationToken cancellationToken) {
            var result = default(TResult);
            if (Async) {
                var function = FunctionAsync;
                if (function != null) {
                    result = await function(cancellationToken);
                }
            }
            else {
                var function = Function;
                if (function != null) {
                    result = function();
                }
            }
            return result;
        }
    }
}
