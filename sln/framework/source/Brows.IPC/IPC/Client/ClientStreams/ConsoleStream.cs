using System;
using System.Threading.Tasks;

namespace Brows.IPC.Client.ClientStreams;

internal sealed class ConsoleStream : ClientStream {
    public sealed override Task<string> Read(CancellationToken token) {
        return Task.Run(cancellationToken: token, function: () => {
            return Console.ReadLine();
        });
    }

    public sealed override Task Write(string value, CancellationToken token) {
        return Task.Run(cancellationToken: token, action: () => {
            Console.WriteLine(value);
        });
    }
}
