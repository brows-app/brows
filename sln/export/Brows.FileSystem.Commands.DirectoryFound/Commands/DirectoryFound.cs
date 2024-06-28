using Domore.Logs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class DirectoryFound : Command {
        private static readonly ILog Log = Logging.For(typeof(DirectoryFound));

        private readonly Option Opt = new();
        private readonly Dictionary<string, DirectoryCache> Cache = new(StringComparer.OrdinalIgnoreCase);

        private ICommandSuggestion Suggestion(ICommandContext context, DirectoryItem item) {
            return Suggestion(
                context: context,
                description: item.Path,
                group: nameof(DirectoryFound),
                groupOrder: Opt.SuggestionGroupOrder,
                help: item.Name,
                input: item.Path,
                relevance: context.HasInput(out var input)
                    ? item.Name.Length - input.Length
                    : null);
        }

        protected sealed override CommandConfig Config => Opt;

        protected sealed override async IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, [EnumeratorCancellation] CancellationToken token) {
            if (context is null) yield break;
            if (context.DidTrigger(out _)) yield break;
            if (context.HasInput(out var input) == false) {
                yield break;
            }
            var mightBeEnvVar = input.Length > 0 && input[0] == '%';
            if (mightBeEnvVar) {
                var inp = input.Length > 2 && input.EndsWith('%')
                    ? input.Substring(1, input.Length - 2)
                    : input.Length > 1
                        ? input.Substring(1)
                        : "";
                var envVars = await DirectoryX.EnvironmentVariablePaths(token);
                foreach (var envVar in envVars) {
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    var name = envVar.Key;
                    var suggest = name.StartsWith(inp, StringComparison.OrdinalIgnoreCase);
                    if (suggest) {
                        yield return Suggestion(context, new DirectoryItem { Name = name, Path = envVar.Value });
                    }
                }
                yield break;
            }
            var lengthIsSufficient = input.Length >= Opt.InputLengthTrigger;
            if (lengthIsSufficient == false) {
                yield break;
            }
            var isInvalid = DirectoryX.Invalid(input);
            if (isInvalid) {
                yield break;
            }
            var mightBePath = DirectoryX.Separated(input);
            if (mightBePath) {
                yield break;
            }
            if (Cache.TryGetValue(input, out var cache)) {
                var cacheAge = cache.Age;
                var cacheExpired = cacheAge > Opt.CacheDuration;
                if (Log.Info()) {
                    Log.Info(Log.Join(nameof(input), input, nameof(cache), cacheAge, nameof(cacheExpired), cacheExpired));
                }
                if (cacheExpired == false) {
                    foreach (var item in cache.Items) {
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        yield return Suggestion(context, item);
                    }
                    yield break;
                }
            }
            var suggestionChannel = Channel.CreateUnbounded<DirectoryItem>(new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });
            var suggestionError = default(Exception);
            var suggestionWriter = suggestionChannel.Writer;
            _ = Task.Run(cancellationToken: token, function: async () => {
                try {
                    var bag = new ConcurrentBag<DirectoryItem>();
                    var drives = DriveInfo.GetDrives();
                    var driveTasks = drives
                        .Where(drive => drive.IsReady)
                        .Select(drive => Task.Run(cancellationToken: token, function: async () => {
                            try {
                                if (Log.Debug()) {
                                    Log.Debug(Log.Join(nameof(input), input, nameof(drive), drive?.Name));
                                }
                                var options = new EnumerationOptions {
                                    AttributesToSkip = FileAttributes.Offline | FileAttributes.ReparsePoint | FileAttributes.System,
                                    IgnoreInaccessible = true,
                                    MatchCasing = MatchCasing.CaseInsensitive,
                                    RecurseSubdirectories = true,
                                    ReturnSpecialDirectories = false
                                };
                                var root = drive.RootDirectory;
                                var dirs = root
                                    .EnumerateDirectories($"*{input}*", options)
                                    .Select(info => DirectoryItem.From(info));
                                foreach (var dir in dirs) {
                                    if (token.IsCancellationRequested) {
                                        if (Log.Debug()) {
                                            Log.Debug(Log.Join(nameof(input), input, nameof(token.IsCancellationRequested), token.IsCancellationRequested));
                                        }
                                        token.ThrowIfCancellationRequested();
                                    }
                                    await suggestionWriter
                                        .WriteAsync(dir, token)
                                        .ConfigureAwait(false);
                                    bag.Add(dir);
                                }
                            }
                            catch (OperationCanceledException canceled) when (canceled?.CancellationToken == token) {
                                throw;
                            }
                            catch (Exception ex) {
                                if (Log.Warn()) {
                                    Log.Warn(ex);
                                }
                            }
                        }));
                    await Task
                        .WhenAll(driveTasks)
                        .ConfigureAwait(false);
                    Cache[input] = new(input, bag);
                }
                catch (Exception ex) {
                    var cacheRemoved = Cache.Remove(input);
                    if (cacheRemoved) {
                        if (Log.Info()) {
                            Log.Info(Log.Join(nameof(input), input, nameof(cacheRemoved)));
                        }
                    }
                    suggestionError = ex;
                }
                finally {
                    if (Log.Info()) {
                        Log.Info(Log.Join(nameof(input), input, nameof(suggestionWriter.Complete), suggestionError?.Message));
                    }
                    suggestionWriter.Complete(suggestionError);
                }
            });
            await foreach (var item in suggestionChannel.Reader.ReadAllAsync(token).ConfigureAwait(false)) {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                yield return Suggestion(context, item);
            }
        }

        private sealed class Option : CommandConfig {
            public int InputLengthTrigger { get; set; } = 4;
            public int SuggestionGroupOrder { get; set; } = 1000;
            public TimeSpan CacheDuration { get; set; } = TimeSpan.FromSeconds(10);
        }

        private sealed class DirectoryItem {
            public string Path { get; init; }
            public string Name { get; init; }

            public static DirectoryItem From(DirectoryInfo info) {
                if (null == info) throw new ArgumentNullException(nameof(info));
                return new DirectoryItem {
                    Path = info.FullName,
                    Name = info.Name
                };
            }
        }

        private sealed class DirectoryCache {
            public TimeSpan Age => DateTime.UtcNow - Birth;

            public string Key { get; }
            public DateTime Birth { get; }
            public IEnumerable<DirectoryItem> Items { get; }

            public DirectoryCache(string key, IEnumerable<DirectoryItem> items) {
                Key = key;
                Birth = DateTime.UtcNow;
                Items = new List<DirectoryItem>(items);
            }
        }
    }
}
