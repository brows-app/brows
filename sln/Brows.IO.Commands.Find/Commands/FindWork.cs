using Domore.Collections.Generic;
using Domore.IO;
using Domore.Logs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Config;
    using Threading.Tasks;

    internal class FindWork {
        private static readonly ILog Log = Logging.For(typeof(FindWork));

        private CancellationTokenSource Cancellation;

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<FindWork>());
        private TaskHandler _TaskHandler;

        private void Result_Died(object sender, EventArgs e) {
            Cancellation?.Cancel();
        }

        private async Task WorkAsync(CancellationToken cancellationToken) {
            var pattern = Parameter.Pattern?.Trim() ?? "";
            if (pattern != "") {
                var config = await Configure.File<FindConfig>().Load(cancellationToken);
                var findIn = Parameter.In;
                var findInDir = findIn.HasFlag(FindIn.DirectoryName);
                var findInFil = findIn.HasFlag(FindIn.FileName);
                var algo = MatchAlgorithm.Create(ignoreCase: !Parameter.CaseSensitive);
                var match = algo.Matcher(pattern);
                var infos = Root.EnumerateFileSystemInfosBreadthFirst(
                    searchPattern: "*",
                    enumerationOptions: new EnumerationOptions {
                        AttributesToSkip = 0,
                        ReturnSpecialDirectories = false
                    });
                var collect = infos.CollectAsync(cancellationToken: cancellationToken, options: CollectAsync.Options(
                    source: infos,
                    skip: info =>
                        (findInFil == false && info is FileInfo) ||
                        (findInDir == false && info is DirectoryInfo),
                    ticks: config.CollectAfterTicks,
                    transform: info => match.Matches(info.Name)
                        ? new FindItem(info, Root)
                        : null
                ));
                var items = collect.FlattenAsync(cancellationToken: cancellationToken, options: new FlattenAsyncOptions {
                    DelayAfterTicks = config.DelayAfterTicks,
                    ChunkSize = config.ChunkSize,
                    DelayMilliseconds = config.DelayMilliseconds
                });
                await foreach (var item in items) {
                    if (item != null) {
                        Result.Add(item);
                        Result.MatchMatched++;
                    }
                    Result.MatchTried++;
                }
            }
        }

        public FindResult Result { get; }
        public Find.Parameter Parameter { get; }
        public DirectoryInfo Root { get; }

        public FindWork(DirectoryInfo root, FindResult result, Find.Parameter parameter) {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        }

        public void Work() {
            TaskHandler.Begin(async () => {
                using (var cancellation = Cancellation = new CancellationTokenSource()) {
                    Result.Died += Result_Died;
                    try {
                        await WorkAsync(cancellation.Token);
                    }
                    catch (Exception ex) {
                        Result.Exception = ex;
                    }
                    finally {
                        Result.Died -= Result_Died;
                        Result.Complete = true;
                        Result.Canceled = cancellation.IsCancellationRequested;
                    }
                }
            });
        }
    }
}
