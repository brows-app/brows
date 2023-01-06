using System;
using System.Threading;
using System.Threading.Channels;

namespace Domore.Threading.Channels {
    internal class ChannelWorkWriter {
        public CancellationToken CancellationToken { get; }

        public ChannelWorkWriter(CancellationToken cancellationToken) {
            CancellationToken = cancellationToken;
        }
    }

    internal class ChannelWorkWriter<T> : ChannelWorkWriter {
        public ChannelWriter<T> Channel { get; }

        public ChannelWorkWriter(ChannelWriter<T> channel, CancellationToken cancellationToken) : base(cancellationToken) {
            Channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        public async void Write(T item) {
            try {
                await Channel.WriteAsync(item, CancellationToken);
            }
            catch (Exception ex) {
                if (ex is OperationCanceledException canceled && canceled.CancellationToken == CancellationToken) {
                    Channel.Complete();
                }
                else {
                    Channel.Complete(ex);
                }
            }
        }
    }
}
