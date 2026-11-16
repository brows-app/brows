using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Tests;

internal sealed class TestRemoteFileSystem {
    private sealed class WriteStream : MemoryStream {
        private readonly TestRemoteFileSystem FileSystem;
        private readonly string Path;
        private bool Disposed;

        protected override void Dispose(bool disposing) {
            var shouldCommit = disposing && !Disposed;
            if (shouldCommit) {
                Disposed = true;
                FileSystem.Write(Path, ToArray());
            }
            base.Dispose(disposing);
        }

        public WriteStream(TestRemoteFileSystem fileSystem, string path) {
            FileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }
    }

    private readonly Dictionary<string, byte[]> Files = new(StringComparer.Ordinal);

    private void Write(string path, byte[] contents) {
        Files[path] = contents ?? throw new ArgumentNullException(nameof(contents));
    }

    public Stream OpenRead(string path) {
        return new RemoteReadStream(this, path);
    }

    public MemoryStream OpenWrite(string path) {
        ArgumentNullException.ThrowIfNull(path);
        Write(path, []);
        return new WriteStream(this, path);
    }

    public void SetFile(Uri uri, string contents) {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(contents);
        Write(Uri.UnescapeDataString(uri.AbsolutePath), System.Text.Encoding.UTF8.GetBytes(contents));
    }

    public string FileContents(Uri uri) {
        ArgumentNullException.ThrowIfNull(uri);
        return System.Text.Encoding.UTF8.GetString(Read(Uri.UnescapeDataString(uri.AbsolutePath)));
    }

    public byte[] Read(string path) {
        ArgumentNullException.ThrowIfNull(path);
        return Files.TryGetValue(path, out var contents)
            ? contents.ToArray()
            : [];
    }

    private sealed class RemoteReadStream : Stream {
        private readonly TestRemoteFileSystem FileSystem;
        private readonly string Path;
        private long ReadPosition;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => FileSystem.Read(Path).Length;

        public override long Position {
            get => ReadPosition;
            set => throw new NotSupportedException();
        }

        public RemoteReadStream(TestRemoteFileSystem fileSystem, string path) {
            FileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public override void Flush() {
        }

        public override int Read(byte[] buffer, int offset, int count) {
            ArgumentNullException.ThrowIfNull(buffer);
            var contents = FileSystem.Read(Path);
            var start = Math.Min((int)ReadPosition, contents.Length);
            var bytesToRead = Math.Min(count, contents.Length - start);
            Array.Copy(contents, start, buffer, offset, bytesToRead);
            ReadPosition += bytesToRead;
            return bytesToRead;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            var contents = FileSystem.Read(Path);
            var start = Math.Min((int)ReadPosition, contents.Length);
            var bytesToRead = Math.Min(buffer.Length, contents.Length - start);
            contents.AsMemory(start, bytesToRead).CopyTo(buffer);
            ReadPosition += bytesToRead;
            return ValueTask.FromResult(bytesToRead);
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }
    }
}
