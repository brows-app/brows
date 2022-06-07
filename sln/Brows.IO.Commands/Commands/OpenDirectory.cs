using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using IO;

    internal class OpenDirectory : Command, ICommandExport {
        private static readonly HashSet<char> InvalidPathChars = new HashSet<char>(Path.GetInvalidPathChars());

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

        private async IAsyncEnumerable<ICommandSuggestion> SuggestChildrenAsync(string input, ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (null == input) throw new ArgumentNullException(nameof(input));
            var parts = input.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var path = string.Join(Path.DirectorySeparatorChar, parts) + Path.DirectorySeparatorChar;
            var dirInfo = await DirectoryInfoExtension.TryNewAsync(path, cancellationToken);
            if (dirInfo == null) yield break;

            var dirInfoExists = false;
            var searchPattern = "*";
            for (; ; ) {
                var exists = dirInfoExists = await dirInfo.ExistsAsync(cancellationToken);
                if (exists) break;

                var parent = await dirInfo.ParentAsync(cancellationToken);
                if (parent == null) break;

                searchPattern = dirInfo.Name + "*";
                dirInfo = parent;
            }

            if (dirInfoExists) {
                var options = SuggestionEnumerationOptions;
                var directories = dirInfo.EnumerateDirectoriesAsync(searchPattern, options, null, cancellationToken);
                await foreach (var directory in directories) {
                    yield return Suggestion(
                        context: context,
                        group: nameof(OpenDirectory),
                        input: await Canon.GetCanonicalFullName(directory, cancellationToken));
                }
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(ICommandContext context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasInput(out var input)) {
                if (await DirectoryAsync.Exists(input, cancellationToken)) {
                    return await context.OpenOrAddPanel(input, cancellationToken);
                }
            }
            return false;
        }

        protected override async IAsyncEnumerable<ICommandSuggestion> ProtectedSuggestAsync(ICommandContext context, [EnumeratorCancellation] CancellationToken cancellationToken) {
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
