using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CHANNEL = System.Threading.Channels.Channel;

namespace Domore.IO {
    public sealed class DirectoryInfoAsyncEnumerable : IAsyncEnumerable<FileSystemInfo> {
        private readonly Channel<FileSystemInfo> Channel = CHANNEL.CreateUnbounded<FileSystemInfo>(new UnboundedChannelOptions {
            SingleReader = true,
            SingleWriter = false
        });

        private bool Locked;
        private readonly object Locker = new();

        private bool Ignore(FileSystemInfo info) {
            var handler = Enumerating;
            if (handler != null) {
                var args = new DirectoryInfoEnumeratingEventArgs(info);
                handler(this, args);
                if (args.Ignore) {
                    return true;
                }
            }
            return false;
        }

        private async Task WriteFilesAndQueueDirectories(DirectoryInfo directoryInfo, EnumerationOptions enumerationOptions, Queue<DirectoryInfo> queue, CancellationToken cancellationToken) {
            if (null == directoryInfo) throw new ArgumentNullException(nameof(directoryInfo));
            if (null == queue) throw new ArgumentNullException(nameof(queue));
            if (false == EnumerateReparsePoints) {
                if (directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                    return;
                }
            }
            var writer = Channel.Writer;
            await Task
                .Run(cancellationToken: cancellationToken, function: async () => {
                    var infos = directoryInfo.EnumerateFileSystemInfos(SearchPattern, enumerationOptions);
                    foreach (var info in infos) {
                        if (cancellationToken.IsCancellationRequested) {
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                        if (Ignore(info)) {
                            continue;
                        }
                        if (info is FileInfo file) {
                            await writer
                                .WriteAsync(file, cancellationToken)
                                .ConfigureAwait(false);
                            Interlocked.Add(ref _FileCount, 1);
                        }
                        else {
                            if (info is DirectoryInfo directory) {
                                queue.Enqueue(directory);
                                Interlocked.Add(ref _DirectoryCount, 1);
                            }
                        }
                    }
                })
                .ConfigureAwait(false);
        }

        private async Task WriteAll(CancellationToken cancellationToken) {
            var opt = EnumerationOptions ?? new();
            var recurse = opt.RecurseSubdirectories;
            var options = new EnumerationOptions {
                AttributesToSkip = opt.AttributesToSkip,
                BufferSize = opt.BufferSize,
                IgnoreInaccessible = opt.IgnoreInaccessible,
                MatchCasing = opt.MatchCasing,
                MatchType = opt.MatchType,
                MaxRecursionDepth = 0,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = opt.ReturnSpecialDirectories
            };
            var writer = Channel.Writer;
            async Task continuation(Queue<DirectoryInfo> queue) {
                var tasks = new List<Task>();
                while (queue.TryDequeue(out var directory)) {
                    await writer
                        .WriteAsync(directory, cancellationToken)
                        .ConfigureAwait(false);
                    if (recurse) {
                        var q = new Queue<DirectoryInfo>();
                        async Task t() {
                            await WriteFilesAndQueueDirectories(directory, options, q, cancellationToken).ConfigureAwait(false);
                            await continuation(q).ConfigureAwait(false);
                        }
                        tasks.Add(t());
                    }
                }
                if (tasks.Count > 0) {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }
            var q = new Queue<DirectoryInfo>();
            await WriteFilesAndQueueDirectories(DirectoryInfo, options, q, cancellationToken).ConfigureAwait(false);
            await continuation(q).ConfigureAwait(false);
        }

        internal DirectoryInfo DirectoryInfo { get; }
        internal string SearchPattern { get; }
        internal EnumerationOptions EnumerationOptions { get; }

        internal DirectoryInfoAsyncEnumerable(DirectoryInfo directoryInfo, string searchPattern, EnumerationOptions enumerationOptions) {
            DirectoryInfo = directoryInfo;
            SearchPattern = searchPattern;
            EnumerationOptions = enumerationOptions;
        }

        public event DirectoryInfoReadyEventHandler Ready;
        public event DirectoryInfoEnumeratingEventHandler Enumerating;

        public long DirectoryCount => Interlocked.Read(ref _DirectoryCount);
        private long _DirectoryCount;

        public long FileCount => Interlocked.Read(ref _FileCount);
        private long _FileCount;

        public bool EnumerateReparsePoints { get; set; }

        public IAsyncEnumerator<FileSystemInfo> GetAsyncEnumerator(CancellationToken cancellationToken) {
            if (Locked) {
                throw new LockedException();
            }
            lock (Locker) {
                if (Locked) {
                    throw new LockedException();
                }
                Locked = true;
            }
            var writer = Channel.Writer;
            var reader = Channel.Reader;
            WriteAll(cancellationToken).ContinueWith(task => {
                writer.Complete(task.Exception);
                Ready?.Invoke(this, new DirectoryInfoReadyEventArgs());
            });
            return reader.ReadAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        }

        private sealed class LockedException : InvalidOperationException {
        }
    }
}
