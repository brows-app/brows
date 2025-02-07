using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.Text.Builders {
    public sealed class TextLineBuilder : DecodedTextBuilder {
        private readonly StringBuilder LineBuilder = new();

        protected sealed override Task Complete(CancellationToken cancellationToken) {
            if (cancellationToken.IsCancellationRequested) {
                return Task.FromCanceled(cancellationToken);
            }
            if (LineBuilder.Length > 0) {
                OnLine?.Invoke(LineBuilder.ToString());
            }
            OnComplete?.Invoke();
            return Task.CompletedTask;
        }

        protected sealed override Task Add(ReadOnlyMemory<char> memory, CancellationToken cancellationToken) {
            if (cancellationToken.IsCancellationRequested) {
                return Task.FromCanceled(cancellationToken);
            }
            var task = Task.CompletedTask;
            var span = memory.Span;
            var start = 0;
            var length = span.Length;
            for (var i = 0; i < length; i++) {
                if (span[i] == '\n') {
                    var end = i > 0 && span[i - 1] == '\r' ? i - 1 : i;
                    if (end == start) {
                        OnLine?.Invoke("");
                    }
                    else {
                        LineBuilder.Append(span[start..end]);
                        OnLine?.Invoke(LineBuilder.ToString());
                        LineBuilder.Clear();
                    }
                    start = i + 1;
                }
            }
            if (start < length) {
                LineBuilder.Append(span[start..length]);
            }
            return Task.CompletedTask;
        }

        protected sealed override Task Clear(CancellationToken cancellationToken) {
            if (cancellationToken.IsCancellationRequested) {
                return Task.FromCanceled(cancellationToken);
            }
            LineBuilder.Clear();
            OnClear?.Invoke();
            return Task.CompletedTask;
        }

        public Action OnClear { get; set; }
        public Action OnComplete { get; set; }
        public Action<string> OnLine { get; set; }

        public TextLineBuilder(Action<string> onLine = null, Action onClear = null, Action onComplete = null) {
            OnLine = onLine;
            OnClear = onClear;
            OnComplete = onComplete;
        }
    }
}
