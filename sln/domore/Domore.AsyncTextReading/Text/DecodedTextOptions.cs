using Domore.Buffers;
using Domore.IO;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domore.Text {
    public class DecodedTextOptions {
        private static readonly ILog Log = Logging.For(typeof(DecodedTextOptions));
        private readonly List<BufferPool> Pools = [];

        private void Free() {
            lock (Pools) {
                foreach (var pool in Pools) {
                    pool.Free();
                }
                Pools.Clear();
            }
        }

        private BufferPool<T> Pool<T>(BufferOptions options) {
            ArgumentNullException.ThrowIfNull(options);
            var pool = options.CreatePool<T>();
            lock (Pools) {
                Pools.Add(pool);
            }
            return pool;
        }

        internal StreamTextDecoder ForStream(DecodedTextDelegate decoded, DecodedTextDelegate completed) {
            return new StreamTextDecoder {
                Completed = completed,
                Decoded = decoded,
                Encoding = Encoding.Count > 0
                    ? Encoding
                    : new[] { "utf-8" },
                EncodingFallback = EncodingFallback,
                StreamBuffer = Pool<byte>(StreamBuffer),
                TextBuffer = Pool<char>(TextBuffer),
            };
        }

        internal StreamTextDecoder ForStream(DecodedTextBuilder builder) {
            return ForStream(
                decoded: builder == null ? null : builder.Decode,
                completed: builder == null ? null : builder.Complete);
        }

        public BufferOptions StreamBuffer {
            get => _StreamBuffer ??= new();
            set => _StreamBuffer = value;
        }
        private BufferOptions _StreamBuffer;

        public BufferOptions TextBuffer {
            get => _TextBuffer ??= new();
            set => _TextBuffer = value;
        }
        private BufferOptions _TextBuffer;

        public Dictionary<string, string> EncodingFallback {
            get => _EncodingFallback ??= [];
            set => _EncodingFallback = value;
        }
        private Dictionary<string, string> _EncodingFallback;

        public List<string> Encoding {
            get => _Encoding ??= [];
            set => _Encoding = value;
        }
        private List<string> _Encoding;

        public IDisposable Disposable() {
            return new DisposableImplementation(this);
        }

        public IAsyncDisposable DisposableAsync() {
            return new DisposableImplementation(this);
        }

        private class DisposableImplementation : IAsyncDisposable, IDisposable {
            protected virtual void Dispose(bool disposing) {
                if (disposing) {
                    try {
                        Options.Free();
                    }
                    catch (Exception ex) {
                        if (Log.Error()) {
                            Log.Error(nameof(Dispose), ex);
                        }
                    }
                }
            }

            protected virtual async ValueTask DisposeAsyncCore() {
                try {
                    var task = Task.Run(() => { using (this) { } });
                    await task.ConfigureAwait(false);
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(nameof(DisposeAsyncCore), ex);
                    }
                }
            }

            public DecodedTextOptions Options { get; }

            public DisposableImplementation(DecodedTextOptions options) {
                Options = options ?? throw new ArgumentNullException(nameof(options));
            }

            void IDisposable.Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            async ValueTask IAsyncDisposable.DisposeAsync() {
                await DisposeAsyncCore().ConfigureAwait(false);
                GC.SuppressFinalize(this);
            }

            ~DisposableImplementation() {
                Dispose(false);
            }
        }
    }
}
