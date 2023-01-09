using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using IO;
    using System.Linq;
    using Threading.Tasks;

    internal class OpenDirectory : Command, ICommandExport {
        private static string Home =>
            _Home ?? (
            _Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify));
        private static string _Home;

        private FileSystemInfoCanon Canon =>
            _Canon ?? (
            _Canon = new FileSystemInfoCanon());
        private FileSystemInfoCanon _Canon;

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

        private Task<(DirectoryInfo Directory, Func<string, string> Replace)> SuggestionRoot(string input, CancellationToken cancellationToken) {
            return Async.With(cancellationToken).Run(() => {
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

        private async IAsyncEnumerable<ICommandSuggestion> SuggestChildrenAsync(string input, ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (null == input) throw new ArgumentNullException(nameof(input));
            var root = await SuggestionRoot(input, cancellationToken);
            var dir = root.Directory;
            if (dir == null) {
                yield break;
            }
            var dirParent = default(DirectoryInfo);
            var dirExists = false;
            var searchPattern = "*";
            for (; ; ) {
                var @break = await Async.With(cancellationToken).Run(() => {
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
                var options = SuggestionEnumerationOptions;
                var directories = dir.EnumerateDirectoriesAsync(searchPattern, options, null, cancellationToken);
                await foreach (var directory in directories) {
                    yield return Suggestion(
                        context: context,
                        group: nameof(OpenDirectory),
                        input: replace(await Canon.GetCanonicalFullName(directory, cancellationToken)));
                }
            }
        }

        protected override async Task<bool> Work(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasInput(out var input)) {
                var exists = await Async.With(cancellationToken).Run(() => {
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
                    return Directory.Exists(input);
                });
                if (exists) {
                    return await context.OpenOrAddPanel(input, cancellationToken);
                }
            }
            return false;
        }

        protected override async IAsyncEnumerable<ICommandSuggestion> Suggest(ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (context == null) yield break;
            if (context.HasInput(out var input)) {
                if (input.Contains(Path.DirectorySeparatorChar) || input.Contains(Path.AltDirectorySeparatorChar)) {
                    await foreach (var suggestion in SuggestChildrenAsync(input, context, cancellationToken)) {
                        yield return suggestion;
                    }
                }
            }
        }

        public override bool Arbitrary => true;
    }
}
