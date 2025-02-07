using Domore.IO;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    internal sealed class ClientStreamText : IStreamText {
        private Stream Stream { get; }

        private ClientStreamText(Stream stream) {
            Stream = stream;
        }

        public static ClientStreamText Create(Stream stream) {
            return new ClientStreamText(stream);
        }

        long IStreamText.StreamLength => throw new NotSupportedException();

        Task<IDisposable> IStreamText.StreamReady(CancellationToken cancellationToken) {
            return Task.FromResult(default(IDisposable));
        }

        Stream IStreamText.StreamText() {
            return Stream;
        }
    }
}
