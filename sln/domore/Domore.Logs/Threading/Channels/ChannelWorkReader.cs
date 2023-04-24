using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;

namespace Domore.Threading.Channels {
    internal class ChannelWorkReader {
        public CancellationToken CancellationToken { get; }

        public ChannelWorkReader(CancellationToken cancellationToken) {
            CancellationToken = cancellationToken;
        }
    }

    internal sealed class ChannelWorkReader<T> : ChannelWorkReader {
        public Action<T> Action { get; set; }
        public ChannelReader<T> Channel { get; }

        public ChannelWorkReader(ChannelReader<T> channel, CancellationToken cancellationToken) : base(cancellationToken) {
            Channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        public async void Read() {
            try {
                await foreach (var item in Channel.ReadAllAsync(CancellationToken)) {
                    try {
                        Action?.Invoke(item);
                    }
                    catch (Exception ex) {
                        try { Debug.WriteLine(ex); } catch { }
                        try { Console.WriteLine(ex); } catch { }
                    }
                }
            }
            catch (OperationCanceledException canceled) when (canceled.CancellationToken == CancellationToken) {
            }
            catch (Exception ex) {
                try { Debug.WriteLine(ex); } catch { }
                try { Console.WriteLine(ex); } catch { }
            }
        }
    }
}
