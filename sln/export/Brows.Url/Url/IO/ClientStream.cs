using Brows.Url;
using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Brows.IO {
    internal sealed class ClientStream : Stream {
        private readonly Channel<byte> Ch = Channel.CreateUnbounded<byte>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        private readonly ChannelWriter<byte> Writer;
        private readonly ChannelReader<byte> Reader;

        private long PrivateLength { get; }
        private Action<ClientDataCallback> OnData { get; }

        private ClientStream(long length, Action<ClientDataCallback> onData) {
            OnData = onData ?? throw new ArgumentNullException(nameof(onData));
            OnData(Callback);
            Writer = Ch.Writer;
            Reader = Ch.Reader;
            PrivateLength = length;
        }

        private async Task<int> ReadPrivate(bool allowAsync, byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            var wrote = 0;
            var reader = Reader;
            for (; ; ) {
                if (wrote == count) {
                    return wrote;
                }
                var read = reader.TryRead(out var byt);
                if (read) {
                    buffer[offset + wrote] = byt;
                    wrote++;
                    continue;
                }
                if (wrote > 0) {
                    return wrote;
                }
                if (reader.Completion.IsCompleted) {
                    return wrote;
                }
                if (allowAsync) {
                    await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
                }
                else {
                    Thread.Sleep(0);
                }
            }
        }

        private void Callback(ReadOnlySpan<byte> data) {
            for (var i = 0; i < data.Length; i++) {
                var written = Writer.TryWrite(data[i]);
                if (written == false) {
                    throw new InvalidOperationException("Write failed");
                }
            }
        }

        public sealed override bool CanRead => true;
        public sealed override bool CanSeek => false;
        public sealed override bool CanWrite => false;
        public sealed override long Length => PrivateLength < 0 ? throw new NotSupportedException() : PrivateLength;
        public sealed override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public void Complete(Exception error) {
            Writer.Complete(error);
        }

        public sealed override void Flush() {
        }

        public sealed override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            return ReadPrivate(allowAsync: true, buffer, offset, count, cancellationToken);
        }

        public sealed override int Read(byte[] buffer, int offset, int count) {
            var read = ReadPrivate(allowAsync: false, buffer, offset, count, default);
            return read.Result;
        }

        public sealed override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public sealed override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public sealed override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }

        public static ClientStream Create(ClientForUrl client, ClientStreamKind kind, long length) {
            ArgumentNullException.ThrowIfNull(client);
            return new ClientStream(length, kind switch {
                ClientStreamKind.Header => client.OnHeader,
                ClientStreamKind.Write => client.OnWrite,
                _ => throw new ArgumentOutOfRangeException(paramName: nameof(kind))
            });
        }
    }
}
