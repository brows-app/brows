using System;

namespace Brows.Threading {
    internal class StaThreadWorkItem<TResult> {
        public Func<TResult> Function { get; }

        public StaThreadWorkItem(Func<TResult> function) {
            Function = function;
        }

        public TResult Invoke() {
            var function = Function;
            return function == null
                ? default
                : function();
        }
    }
}
