using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.Text.Builders {
    public sealed class TextStringBuilder : DecodedTextBuilder {
        private string StringComplete;
        private readonly StringBuilder StringBuilder = new();

        protected sealed override Task Complete(CancellationToken cancellationToken) {
            if (cancellationToken.IsCancellationRequested) {
                return Task.FromCanceled(cancellationToken);
            }
            StringComplete = StringBuilder.ToString();
            return Task.CompletedTask;
        }

        protected sealed override Task Add(ReadOnlyMemory<char> memory, CancellationToken cancellationToken) {
            if (cancellationToken.IsCancellationRequested) {
                return Task.FromCanceled(cancellationToken);
            }
            StringBuilder.Append(memory);
            return Task.CompletedTask;
        }

        protected sealed override Task Clear(CancellationToken cancellationToken) {
            if (cancellationToken.IsCancellationRequested) {
                return Task.FromCanceled(cancellationToken);
            }
            StringBuilder.Clear();
            return Task.CompletedTask;
        }

        public sealed override string ToString() {
            return StringComplete ?? StringBuilder.ToString();
        }
    }
}
