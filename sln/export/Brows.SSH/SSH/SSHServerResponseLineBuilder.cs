using Domore.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Brows.SSH {
    internal sealed class SSHServerResponseLineBuilder : DecodedTextBuilder {
        private readonly StringBuilder LineBuilder = new StringBuilder();
        private readonly Channel<string> LineChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions {
            SingleReader = true,
            SingleWriter = true
        });

        protected sealed override async Task Complete(CancellationToken token) {
            if (LineBuilder.Length > 0) {
                var line =
                LineBuilder.ToString();
                LineBuilder.Clear();
                await LineChannel.Writer.WriteAsync(line, token);
            }
            LineChannel.Writer.Complete();
            await Task.CompletedTask;
        }

        protected sealed override async Task Add(ReadOnlyMemory<char> memory, CancellationToken token) {
            var chars = memory.ToArray();
            await Task.WhenAll(chars.Select(c => {
                if (c == '\n') {
                    var line =
                    LineBuilder.ToString();
                    LineBuilder.Clear();
                    return LineChannel.Writer.WriteAsync(line, token).AsTask();
                }
                else {
                    LineBuilder.Append(c);
                    return Task.CompletedTask;
                }
            }));
        }

        protected sealed override async Task Clear(CancellationToken token) {
            Reset?.Invoke(this, EventArgs.Empty);
            await Task.CompletedTask;
        }

        public event EventHandler Reset;

        public async IAsyncEnumerable<string> Lines([EnumeratorCancellation] CancellationToken token) {
            await foreach (var line in LineChannel.Reader.ReadAllAsync(token)) {
                yield return line;
            }
        }
    }
}
