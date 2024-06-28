using Domore.IPC;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.Net.Sockets {
    internal sealed class TcpMessenger : Messenger {
        private TcpClientServer ClientServer => _ClientServer ??= new TcpClientServer(Factory.Directory);
        private TcpClientServer _ClientServer;

        public MessengerFactory Factory { get; }

        public TcpMessenger(MessengerFactory factory) {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public sealed override async Task Send(string message, CancellationToken cancellationToken) {
            var writer = await ClientServer.ConnectWriter(cancellationToken).ConfigureAwait(false);
            if (writer != null) {
                using (writer) {
                    await writer.Write(message, cancellationToken).ConfigureAwait(false);
                    Factory.OnMessageSent(message);
                }
            }
        }

        public sealed override async IAsyncEnumerable<string> Receive([EnumeratorCancellation] CancellationToken cancellationToken) {
            using (var reader = await ClientServer.StartReader(cancellationToken).ConfigureAwait(false)) {
                var exit = false;
                for (; ; ) {
                    if (cancellationToken.IsCancellationRequested) {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    if (exit) {
                        break;
                    }
                    var reads = reader.ReadClient(cancellationToken);
                    await using (var read = reads.GetAsyncEnumerator(cancellationToken)) {
                        var next = false;
                        try {
                            next = await read.MoveNextAsync().ConfigureAwait(false);
                        }
                        catch (OperationCanceledException canceled) when (canceled.CancellationToken == cancellationToken) {
                            throw;
                        }
                        catch (Exception ex) {
                            var e = Factory.OnError(ex);
                            exit = e?.Exit ?? false;
                        }
                        if (next) {
                            var message = read.Current;
                            Factory.OnMessageReceived(message);
                            yield return message;
                        }
                    }
                }
            }
        }
    }
}
