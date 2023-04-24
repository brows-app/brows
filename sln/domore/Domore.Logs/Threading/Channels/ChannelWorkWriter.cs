using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;

namespace Domore.Threading.Channels {
    internal class ChannelWorkWriter {
        public CancellationToken CancellationToken { get; }

        public ChannelWorkWriter(CancellationToken cancellationToken) {
            CancellationToken = cancellationToken;
        }
    }

    internal sealed class ChannelWorkWriter<T> : ChannelWorkWriter {
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
                }
                else {
                    try { Debug.WriteLine(ex); } catch { }
                    try { Console.WriteLine(ex); } catch { }
                }
                try {
                    Channel.Complete(ex);
                }
                catch (Exception ex2) {
                    try { Debug.WriteLine(ex2); } catch { }
                    try { Console.WriteLine(ex2); } catch { }
                }
            }
        }
    }
}
