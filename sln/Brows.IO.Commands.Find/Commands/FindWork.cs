using Domore.Logs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using IO;
    using Threading.Tasks;

    internal class FindWork {
        private static readonly ILog Log = Logging.For(typeof(FindWork));

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<FindWork>());
        private TaskHandler _TaskHandler;

        private void Result_Died(object sender, EventArgs e) {
            Cancel = true;
        }

        private async Task WorkAsync() {
            var pattern = Parameter.Pattern?.Trim() ?? "";
            if (pattern != "") {
                using (var cancellationSource = new CancellationTokenSource()) {
                    var findIn = Parameter.In;
                    var findInDir = findIn.HasFlag(FindIn.DirectoryName);
                    var findInFil = findIn.HasFlag(FindIn.FileName);
                    var infos = Root.RecurseByDepthAsync(cancellationSource.Token);
                    var algo = MatchAlgorithm.Create(ignoreCase: !Parameter.CaseSensitive);
                    var matcher = algo.Matcher(pattern);
                    await foreach (var info in infos) {
                        if (Cancel) {
                            break;
                        }
                        if (findInFil == false && info is FileInfo) {
                            continue;
                        }
                        if (findInDir == false && info is DirectoryInfo) {
                            continue;
                        }
                        var name = info.Name;
                        var matched = matcher.Matches(name);
                        if (matched) {
                            Result.Add(new FindItem(info, Root));
                            Result.MatchMatched++;
                        }
                        Result.MatchTried++;
                    }
                    if (Cancel) {
                        if (Log.Info()) {
                            Log.Info(nameof(Cancel));
                        }
                        cancellationSource.Cancel();
                    }
                }
            }
        }

        public FindResult Result { get; }
        public Find.Parameter Parameter { get; }
        public DirectoryInfo Root { get; }
        public bool Cancel { get; private set; }

        public FindWork(DirectoryInfo root, FindResult result, Find.Parameter parameter) {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        }

        public void Work() {
            TaskHandler.Begin(async () => {
                Result.Died += Result_Died;
                try {
                    await WorkAsync();
                }
                catch (Exception ex) {
                    Result.Exception = ex;
                }
                finally {
                    Result.Died -= Result_Died;
                    Result.Complete = true;
                    Result.Canceled = Cancel;
                }
            });
        }
    }
}
