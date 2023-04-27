using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Brows {
    internal abstract class FileSystemReader {
        private static readonly ILog Log = Logging.For(typeof(FileSystemReader));
        private static readonly EnumerationOptions EnumerationOptions = new EnumerationOptions {
            AttributesToSkip = 0,
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };

        private CancellationTokenSource WaitSource;

        private void Completed(bool complete) {
            try { WaitSource?.Cancel(); }
            catch { }
        }

        public bool Complete { get; private set; }

        public async Task Wait(int timeout, CancellationToken token) {
            if (token.IsCancellationRequested) {
                token.ThrowIfCancellationRequested();
            }
            if (Complete) {
                return;
            }
            using var source = WaitSource = new CancellationTokenSource();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(source.Token, token);
            try {
                await Task.Delay(timeout, linked.Token);
            }
            catch (OperationCanceledException canceled) when (canceled.CancellationToken == linked.Token) {
                if (Log.Info()) {
                    Log.Info(Log.Join(nameof(Wait), nameof(canceled)));
                }
            }
            if (token.IsCancellationRequested) {
                token.ThrowIfCancellationRequested();
            }
            WaitSource = null;
        }

        public static FileSystemReader<T> Read<T>(DirectoryInfo directory, Func<FileSystemInfo, T> transform, CancellationToken token) {
            if (directory is null) throw new ArgumentNullException(nameof(directory));
            if (transform is null) throw new ArgumentNullException(nameof(transform));
            var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions {
                SingleWriter = true
            });
            var writer = channel.Writer;
            var reader = FileSystemReader<T>.Create(channel.Reader);
            Task.Run(cancellationToken: token, function: async () => {
                var error = default(Exception);
                try {
                    foreach (var info in directory.EnumerateFileSystemInfos("*", EnumerationOptions)) {
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        var ignore =
                            info.Attributes.HasFlag(FileAttributes.Hidden) &&
                            info.Attributes.HasFlag(FileAttributes.System) &&
                            info.Attributes.HasFlag(FileAttributes.Directory);
                        if (ignore) {
                            continue;
                        }
                        var item = transform(info);
                        if (item != null) {
                            await writer.WriteAsync(item);
                        }
                    }
                }
                catch (Exception ex) {
                    error = ex;
                }
                finally {
                    writer.Complete(error);
                    reader.Complete = true;
                    reader.Completed(true);
                }
            });
            return reader;
        }
    }

    internal sealed class FileSystemReader<T> : FileSystemReader {
        private ChannelReader<T> Reader { get; }

        private FileSystemReader(ChannelReader<T> reader) {
            Reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        public IReadOnlyList<T> Existing() {
            var list = new List<T>();
            for (; ; ) {
                if (Reader.TryRead(out var item)) {
                    list.Add(item);
                }
                else {
                    break;
                }
            }
            return list;
        }

        public IAsyncEnumerable<T> Remaining(CancellationToken token) {
            return Reader.ReadAllAsync(token);
        }

        public static FileSystemReader<T> Create(ChannelReader<T> reader) {
            return new FileSystemReader<T>(reader);
        }
    }
}
