using System;
using System.Threading;
using System.Threading.Channels;

namespace Domore.Threading.Channels {
    internal class ChannelWorkReader {
        public CancellationToken CancellationToken { get; }

        public ChannelWorkReader(CancellationToken cancellationToken) {
            CancellationToken = cancellationToken;
        }
    }

    internal class ChannelWorkReader<T> : ChannelWorkReader {
        public Action<T> Action { get; set; }
        public ChannelReader<T> Channel { get; }

        public ChannelWorkReader(ChannelReader<T> channel, CancellationToken cancellationToken) : base(cancellationToken) {
            Channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        public async void Read() {
            try {
                while (await Channel.WaitToReadAsync(CancellationToken)) {
                    while (Channel.TryRead(out var item)) {
                        Action?.Invoke(item);
                    }
                }
            }
            catch (Exception ex) {
                if (ex is OperationCanceledException canceled && canceled.CancellationToken == CancellationToken) {
                }
            }
        }
    }
}
