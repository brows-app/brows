using Domore.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class OpenDirectory : Command {
        private static string Home =>
            _Home ?? (
            _Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify));
        private static string _Home;

        private EnumerationOptions SuggestionEnumerationOptions =>
            _SuggestionEnumerationOptions ?? (
            _SuggestionEnumerationOptions = new EnumerationOptions {
                AttributesToSkip = FileAttributes.Offline | FileAttributes.ReparsePoint | FileAttributes.System,
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.CaseInsensitive,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false
            });
        private EnumerationOptions _SuggestionEnumerationOptions;

        private Task<(DirectoryInfo Directory, Func<string, string> Replace)> SuggestionRoot(string input, CancellationToken token) {
            return Task.Run(cancellationToken: token, function: () => {
                var replace = new Func<string, string>(s => s);
                var parts = input.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 0) {
                    return (null, replace);
                }
                if (parts[0] == "~") {
                    var home = Home;
                    parts[0] = home;
                    replace = s => "~" + s.Substring(home.Length);
                }
                else {
                    var envVar = parts[0];
                    if (envVar.StartsWith('%') && envVar.EndsWith('%')) {
                        var expand = Environment.ExpandEnvironmentVariables(envVar);
                        parts[0] = expand;
                        replace = s => envVar + s.Substring(expand.Length);
                    }
                }
                var path = string.Join(Path.DirectorySeparatorChar, parts) + Path.DirectorySeparatorChar;
                var dirInfo = default(DirectoryInfo);
                try {
                    dirInfo = new DirectoryInfo(path);
                }
                catch {
                    return (null, replace);
                }
                return (dirInfo, replace);
            });
        }

        private IAsyncEnumerable<string> EnumerateDirectories(DirectoryInfo directory, string searchPattern, EnumerationOptions enumerationOptions, CancellationToken token) {
            if (null == directory) throw new ArgumentNullException(nameof(directory));
            var directories = directory.EnumerateDirectories(searchPattern, enumerationOptions);
            var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
            var writer = channel.Writer;
            var reader = channel.Reader;
            async Task write(DirectoryInfo directory, CancellationToken token) {
                if (directory == null) {
                    return;
                }
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                var path = await directory.CorrectCasingAsync(token);
                await writer.WriteAsync(path, token);
            }
            Task.Run(cancellationToken: token, function: async () => {
                var error = default(Exception);
                try {
                    await Task.WhenAll(directories.Select(directory => write(directory, token)));
                }
                catch (Exception ex) {
                    error = ex;
                }
                finally {
                    writer.Complete(error);
                }
            });
            return reader.ReadAllAsync(token);
        }

        private async IAsyncEnumerable<ICommandSuggestion> SuggestChildrenAsync(string input, ICommandContext context, [EnumeratorCancellation] CancellationToken token) {
            if (null == input) throw new ArgumentNullException(nameof(input));
            var root = await SuggestionRoot(input, token);
            var dir = root.Directory;
            if (dir == null) {
                yield break;
            }
            var dirParent = default(DirectoryInfo);
            var dirExists = false;
            var searchPattern = "*";
            for (; ; ) {
                var @break = await Task.Run(cancellationToken: token, function: () => {
                    var exists = dirExists = dir.Exists;
                    if (exists) {
                        return true;
                    }
                    var parent = dirParent = dir.Parent;
                    if (parent == null) {
                        return true;
                    }
                    return false;
                });
                if (@break) {
                    break;
                }
                searchPattern = dir.Name + "*";
                dir = dirParent;
            }
            if (dirExists) {
                var replace = root.Replace;
                var directories = EnumerateDirectories(dir, searchPattern, SuggestionEnumerationOptions, token);
                await foreach (var directory in directories) {
                    yield return Suggestion(
                        context: context,
                        group: nameof(OpenDirectory),
                        input: replace(directory));
                }
            }
        }

        protected sealed override bool ArbitraryWork(ICommandContext context) {
            if (context == null) return false;
            if (context.HasInput(out var input) == false) {
                return false;
            }
            var couldBeHome = input.StartsWith('~');
            if (couldBeHome) {
                var isHome = input == "~" || input.StartsWith("~" + Path.DirectorySeparatorChar) || input.StartsWith("~" + Path.AltDirectorySeparatorChar);
                if (isHome) {
                    input = Home + input.Substring(1);
                }
            }
            else {
                var couldBeEnv = input.StartsWith('%');
                if (couldBeEnv) {
                    var env = new string(input.TakeWhile(c => c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar).ToArray());
                    var isEnv = env.EndsWith('%');
                    if (isEnv) {
                        input = Environment.ExpandEnvironmentVariables(env) + input.Substring(env.Length);
                    }
                }
            }
            var invalid = Path.GetInvalidPathChars().Any(c => input.Contains(c));
            if (invalid) {
                return false;
            }
            var fullyQualified = Path.IsPathFullyQualified(input);
            if (fullyQualified == false) {
                return false;
            }
            var exists = Directory.Exists(input);
            if (exists == false) {
                return false;
            }
            return context.Operate(async (progress, token) => {
                return await context.Provide(input, CommandContextProvide.ActivePanel, token);
            });
        }

        protected sealed override async IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, [EnumeratorCancellation] CancellationToken token) {
            if (context == null) yield break;
            if (context.DidTrigger(out _)) yield break;
            if (context.HasInput(out var input) == false) {
                yield break;
            }
            var mightBePath = input.Contains(Path.DirectorySeparatorChar) || input.Contains(Path.AltDirectorySeparatorChar);
            if (mightBePath == false) {
                yield break;
            }
            await foreach (var suggestion in SuggestChildrenAsync(input, context, token)) {
                yield return suggestion;
            }
        }
    }
}
