using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Domore.Text.Builders {
    public sealed class TextStreamBuilder : DecodedTextBuilder {
        private readonly object ReadLocker = new();
        private readonly ChannelReader<TextStreamItem> Reader;
        private readonly ChannelWriter<TextStreamItem> Writer;

        private bool Reading;

        protected sealed override async Task Add(ReadOnlyMemory<char> memory, CancellationToken cancellationToken) {
            var s = new string(memory.Span);
            var i = new TextStreamItem(s);
            await Writer.WriteAsync(i, cancellationToken);
        }

        protected sealed override async Task Clear(CancellationToken cancellationToken) {
            var s = default(string);
            var i = new TextStreamItem(s);
            await Writer.WriteAsync(i, cancellationToken);
        }

        protected sealed override Task Complete(CancellationToken cancellationToken) {
            if (cancellationToken.IsCancellationRequested) {
                return Task.FromCanceled(cancellationToken);
            }
            Writer.Complete();
            return Task.CompletedTask;
        }

        public TextStreamBuilder() {
            var channel = Channel.CreateUnbounded<TextStreamItem>(new() { SingleWriter = true, SingleReader = true });
            Reader = channel.Reader;
            Writer = channel.Writer;
        }

        public IAsyncEnumerable<TextStreamItem> Read(CancellationToken cancellationToken) {
            lock (ReadLocker) {
                if (Reading == false) {
                    Reading = true;
                }
                else {
                    throw new InvalidOperationException();
                }
            }
            return Reader.ReadAllAsync(cancellationToken);
        }
    }
}
