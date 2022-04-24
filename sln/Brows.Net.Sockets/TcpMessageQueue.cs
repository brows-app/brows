using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Net.Sockets;

    public class TcpMessageQueue : ICommanderMessageQueue {
        private TcpClientServer ClientServer =>
            _ClientServer ?? (
            _ClientServer = new TcpClientServer(nameof(TcpMessageQueue)));
        private TcpClientServer _ClientServer;

        public async Task Write(string s, CancellationToken cancellationToken) {
            var writer = await ClientServer.CreateWriter(cancellationToken);
            if (writer != null) {
                using (writer) {
                    await writer.Connect(cancellationToken);
                    await writer.Write(s, cancellationToken);
                }
            }
        }

        public async IAsyncEnumerable<string> Read([EnumeratorCancellation] CancellationToken cancellationToken) {
            for (; ; ) {
                using (var reader = await ClientServer.CreateReader(cancellationToken)) {
                    reader.Start();
                    try {
                        for (; ; ) {
                            await foreach (var item in reader.ReadClient(cancellationToken)) {
                                if (item != null) {
                                    yield return item;
                                }
                                break;
                            }
                        }
                    }
                    finally {
                        reader.Stop();
                    }
                }
            }
        }
    }
}
