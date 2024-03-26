using Domore.Logs;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.SSH.Native {
    internal abstract class ChanStream : Stream {
        private static readonly ILog Log = Logging.For(typeof(ChanStream));

        private int PrivateRead(byte[] buffer, int offset, int count, CancellationToken token) {
            if (buffer is null) throw new ArgumentNullException(nameof(buffer));
            if (buffer.Length < (offset + count)) throw new ArgumentException();

            if (count < 0) throw new ArgumentOutOfRangeException(paramName: nameof(count));
            if (offset < 0) throw new ArgumentOutOfRangeException(paramName: nameof(offset));
#if DEBUG
            if (Log.Debug()) {
                Log.Debug($"{nameof(PrivateRead)} length:[{buffer.Length}] offset:[{offset}] count:[{count}]");
            }
#endif
            var pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try {
                var address = pin.AddrOfPinnedObject();
                var pointer = address + offset;
#if DEBUG
                if (Log.Debug()) {
                    Log.Debug($"{nameof(PrivateRead)} address:[{address}] pointer:[{pointer}]");
                }
#endif
                var canceler = BrowsCanceler.From(token);
                for (; ; ) {
                    var read = NativeRead(Chan.GetHandle(), StreamID, pointer, (nuint)count, canceler);
#if DEBUG
                    if (Log.Debug()) {
                        Log.Debug($"{nameof(PrivateRead)} read:[{read}]");
                    }
#endif
                    if (read == 0) {
                        if (count == 0) {
                            return 0;
                        }
                        var eof = Chan.EOF(canceler);
                        if (eof) {
                            return 0;
                        }
                        //continue;
                        //Chan.EOF is returning false even thought I think it should be true...
                        return 0;
                    }
                    return (int)read;
                }
            }
            finally {
                pin.Free();
            }
        }

        private void PrivateWrite(byte[] buffer, int offset, int count, CancellationToken token) {
#if DEBUG
            if (Log.Debug()) {
                Log.Debug($"{nameof(PrivateWrite)} length:[{buffer?.Length}] offset:[{offset}] count:[{count}]");
            }
#endif
            var pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try {
                var address = pin.AddrOfPinnedObject();
                var pointer = address + offset;
#if DEBUG
                if (Log.Debug()) {
                    Log.Debug($"{nameof(PrivateWrite)} address:[{address}] pointer:[{pointer}]");
                }
#endif
                var canceler = BrowsCanceler.From(token);
                var totalWrite = default(nint);
                var originalCount = count;
                for (; ; ) {
                    var write = NativeWrite(Chan.GetHandle(), StreamID, pointer, (nuint)count, canceler);
#if DEBUG
                    if (Log.Debug()) {
                        Log.Debug($"{nameof(PrivateWrite)} write:[{write}]");
                    }
#endif
                    var thisWrite = (nint)write;
                    var totalWritten = totalWrite += thisWrite;
                    if (totalWritten >= originalCount) {
                        break;
                    }
                    count -= (int)thisWrite;
                    pointer += thisWrite;
#if DEBUG
                    if (Log.Debug()) {
                        Log.Debug($"{nameof(PrivateWrite)} pointer:[{pointer}] tally:[{totalWrite}] remain:[{count}]");
                    }
#endif
                }
            }
            finally {
                pin.Free();
            }
        }

        protected virtual nuint NativeRead(IntPtr p, int streamID, IntPtr buf, nuint bufLen, BrowsCanceler cancel) {
            throw new NotSupportedException();
        }

        protected virtual nuint NativeWrite(IntPtr p, int streamID, IntPtr buf, nuint bufLen, BrowsCanceler cancel) {
            throw new NotSupportedException();
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length =>
            throw new NotSupportedException();

        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public Chan Chan { get; }
        public int StreamID { get; }

        public ChanStream(Chan chan, int streamID) {
            Chan = chan ?? throw new ArgumentNullException(nameof(chan));
            StreamID = streamID;
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public sealed override void Flush() {
#if DEBUG
            if (Log.Debug()) {
                Log.Debug(nameof(Flush));
            }
#endif
            Chan.Flush(StreamID, BrowsCanceler.None);
        }

        public sealed override int Read(byte[] buffer, int offset, int count) {
            return PrivateRead(buffer, offset, count, CancellationToken.None);
        }

        public sealed override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            return await Task.Run(cancellationToken: cancellationToken, function: () => {
                return PrivateRead(buffer, offset, count, cancellationToken);
            });
        }

        public sealed override void Write(byte[] buffer, int offset, int count) {
            PrivateWrite(buffer, offset, count, CancellationToken.None);
        }

        public sealed override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            await Task.Run(cancellationToken: cancellationToken, action: () => {
                PrivateWrite(buffer, offset, count, cancellationToken);
            });
        }
    }
}
