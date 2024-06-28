using Domore.IO;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class FindWork {
        private static readonly ILog Log = Logging.For(typeof(FindWork));

        private async Task Write(ChannelWriter<FoundInInfo[]> writer, IOperationProgress progress, CancellationToken token) {
            if (null == writer) throw new ArgumentNullException(nameof(writer));
            var param = Parameter;
            var @in = param.In;
            var findIn = new FindInInfo(Root, param);
            var exclude = param.ExcludeFilter().Matchers().ToList();
            var include = param.IncludeFilter().Matchers().ToList();
            await Task
                .Run(cancellationToken: token, function: async () => {
                    var findTasks = new List<Task>();
                    var opts = new EnumerationOptions {
                        AttributesToSkip = 0,
                        ReturnSpecialDirectories = false,
                        RecurseSubdirectories = !@in.HasFlag(FindIn.TopDirectoryOnly)
                    };
                    var
                    enumerable = Root.EnumerateFileSystemInfosAsync("*", opts);
                    enumerable.Enumerating += (s, e) => {
                        var info = e?.FileSystemInfo;
                        if (info == null) {
                            e.Ignore = true;
                            return;
                        }
                        if (exclude.Count > 0) {
                            var name = info.FullName;
                            if (exclude.Any(x => x.Matches(name))) {
                                e.Ignore = true;
                            }
                        }
                    };
                    enumerable.Ready += (s, e) => {
                        if (progress != null) {
                            progress.Change(setTarget: enumerable.FileCount + enumerable.DirectoryCount);
                        }
                    };
                    await foreach (var item in enumerable.WithCancellation(token).ConfigureAwait(false)) {
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        if (include.Count > 0) {
                            var name = item.FullName;
                            if (include.Any(i => i.Matches(name)) == false) {
                                continue;
                            }
                        }
                        var tasks = findIn.Tasks(item, token).ToList();
                        if (tasks.Count > 0) {
                            var progressed = false;
                            async Task findTask() {
                                for (; ; ) {
                                    if (token.IsCancellationRequested) {
                                        token.ThrowIfCancellationRequested();
                                    }
                                    if (tasks.Count == 0) {
                                        break;
                                    }
                                    var task = await Task.WhenAny(tasks).ConfigureAwait(false);
                                    var result = await task.ConfigureAwait(false);
                                    await writer.WriteAsync(result, token).ConfigureAwait(false);
                                    if (progressed == false && progress != null) {
                                        progressed = true;
                                        progress.Change(1);
                                        progress.Change(addProgress: 1, data: item.Name);
                                    }
                                    tasks.Remove(task);
                                }
                            }
                            findTasks.Add(findTask());
                        }
                        else {
                            if (progress != null) {
                                progress.Change(addProgress: 1, data: item.Name);
                            }
                        }
                    }
                    await Task.WhenAll(findTasks).ConfigureAwait(false);
                })
                .ConfigureAwait(false);
        }

        private async void BeginWrite(ChannelWriter<FoundInInfo[]> writer, IOperationProgress progress, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(writer);
            var error = default(Exception);
            try {
                await Write(writer, progress, token).ConfigureAwait(false);
            }
            catch (Exception ex) {
                error = ex;
            }
            writer.Complete(error);
        }

        private async Task Read(ChannelReader<FoundInInfo[]> reader, IOperationProgress progress, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(reader);
            var read = reader.ReadAllAsync(token);
            await foreach (var item in read.ConfigureAwait(false)) {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                if (item != null) {
                    Result.MatchMatched++;
                    Result.Add(item);
                }
                Result.MatchTried++;
            }
        }

        private async Task Work(IOperationProgress progress, CancellationToken token) {
            var channel = Channel.CreateUnbounded<FoundInInfo[]>(new UnboundedChannelOptions {
                SingleReader = true,
                SingleWriter = true
            });
            BeginWrite(channel.Writer, progress, token);
            await Read(channel.Reader, progress, token).ConfigureAwait(false);
        }

        public DirectoryInfo Root { get; }
        public FindParameter Parameter { get; }
        public FileSystemFindResult Result { get; }

        public FindWork(FileSystemFindResult result) {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Root = Result.Root ?? throw new ArgumentException();
            Parameter = Result.Parameter ?? throw new ArgumentException();
        }

        public async Task Done(IOperationProgress progress, CancellationToken token) {
            try {
                await Work(progress, token).ConfigureAwait(false);
            }
            catch (Exception ex) {
                if (ex is OperationCanceledException canceled && canceled.CancellationToken == token) {
                    Result.Canceled = true;
                }
                else {
                    Result.Exception = ex;
                }
            }
            finally {
                Result.Complete = true;
            }
        }
    }
}
