using System;
using System.Threading;

namespace Brows.Collections.ObjectModel {
    public sealed class ObservableSourceDisposable<T> : ObservableSource<T>, IDisposable {
        private readonly CancellationTokenSource TokenSource;

        private async void Begin(CancellationToken token) {
            try {
                await Collect(token);
            }
            catch (OperationCanceledException canceled) {
                if (canceled?.CancellationToken != token) {
                    throw;
                }
            }
        }

        public CancellationToken Token { get; }

        public ObservableSourceDisposable() {
            TokenSource = new();
            Token = TokenSource.Token;
            Begin(Token);
        }

        void IDisposable.Dispose() {
            using (TokenSource) {
                TokenSource.Cancel();
            }
        }
    }
}
