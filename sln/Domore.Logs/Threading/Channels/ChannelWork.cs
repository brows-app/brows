using System;
using System.Threading;
using System.Threading.Channels;

namespace Domore.Threading.Channels {
    internal class ChannelWork {
        public string Name { get; }

        public ChannelWork(string name) {
            Name = name;
        }
    }

    internal class ChannelWork<T> : ChannelWork {
        private readonly object Locker = new object();
        private ChannelWorkWriter<T> Writer;
        private CancellationTokenSource CancellationTokenSource;

        public Action<T> Action { get; }

        public ChannelWork(string name, Action<T> action) : base(name) {
            Action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public void Add(T item) {
            if (Writer == null) {
                lock (Locker) {
                    if (Writer == null) {
                        CancellationTokenSource = new CancellationTokenSource();
                        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
                        var channelReader = new ChannelWorkReader<T>(channel.Reader, CancellationTokenSource.Token) { Action = Action };
                        var channelWriter = new ChannelWorkWriter<T>(channel.Writer, CancellationTokenSource.Token);
                        var channelThread = new Thread(channelReader.Read) { IsBackground = true, Name = Name };
                        channelThread.Start();
                        Writer = channelWriter;
                    }
                }
            }
            Writer.Write(item);
        }

        public void Complete() {
            if (CancellationTokenSource != null) {
                lock (Locker) {
                    if (CancellationTokenSource != null) {
                        CancellationTokenSource.Cancel();
                        CancellationTokenSource.Dispose();
                        CancellationTokenSource = null;
                        Writer = null;
                    }
                }
            }
        }
    }
}
