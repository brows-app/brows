using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Threading {
    internal class StaThreadWorkItem<TResult> {
        public bool IsAsync { get; }
        public Func<TResult> Function { get; }
        public Func<CancellationToken, Task<TResult>> FunctionAsync { get; }

        public StaThreadWorkItem(Func<TResult> function) {
            IsAsync = false;
            Function = function;
        }

        public StaThreadWorkItem(Func<CancellationToken, Task<TResult>> function) {
            IsAsync = true;
            FunctionAsync = function;
        }

        public async Task<TResult> InvokeAsync(CancellationToken cancellationToken) {
            var result = default(TResult);
            if (IsAsync) {
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
