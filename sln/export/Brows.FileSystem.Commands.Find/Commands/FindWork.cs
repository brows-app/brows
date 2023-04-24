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
            await Task.Run(cancellationToken: token, function: async () => {
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
                        progress.Target.Set(enumerable.FileCount + enumerable.DirectoryCount);
                    }
                };
                await foreach (var item in enumerable.WithCancellation(token)) {
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    if (include.Count > 0) {
                        var name = item.FullName;
                        if (include.Any(i => i.Matches(name)) == false) {
                            continue;
                        }
                    }
                    var pItem = false;
                    var tasks = findIn.Begin(info: item, token: token, found: async found => {
                        if (pItem == false && progress != null) {
                            pItem = true;
                            progress.Add(1);
                            progress.Info.Data(item.Name);
                        }

                        await writer.WriteAsync(found, token);
                    });
                    findTasks.AddRange(tasks);
                    findTasks.RemoveAll(task => task.IsCompleted);
                }
                if (findTasks.Count > 0) {
                    await Task.WhenAll(findTasks);
                }
            });
        }

        private async void BeginWrite(ChannelWriter<FoundInInfo[]> writer, IOperationProgress progress, CancellationToken token) {
            if (null == writer) throw new ArgumentNullException(nameof(writer));
            var error = default(Exception);
            try {
                await Write(writer, progress, token);
            }
            catch (Exception ex) {
                error = ex;
            }
            writer.Complete(error);
        }

        private async Task Read(ChannelReader<FoundInInfo[]> reader, IOperationProgress progress, CancellationToken token) {
            if (reader is null) throw new ArgumentNullException(nameof(reader));
            var reads = reader.ReadAllAsync(token);
            var added = new List<Task>();
            await foreach (var item in reads) {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                if (item != null) {
                    Result.MatchMatched++;
                    added.Add(Result.Add(item, token));
                    added.RemoveAll(add => add.IsCompleted);
                }
                Result.MatchTried++;
            }
            if (added.Count > 0) {
                await Task.WhenAll(added);
            }
        }

        private async Task Work(IOperationProgress progress, CancellationToken token) {
            var channel = Channel.CreateUnbounded<FoundInInfo[]>(new UnboundedChannelOptions {
                SingleReader = true,
                SingleWriter = false
            });
            BeginWrite(channel.Writer, progress, token);
            await Read(channel.Reader, progress, token);
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
            using var begun = Result.Begin(Parameter.Observe);
            try {
                await Work(progress, token);
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
