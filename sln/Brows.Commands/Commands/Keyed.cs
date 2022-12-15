using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Brows.Commands {
    internal abstract class Keyed<T> : Command<T> where T : Keyed<T>.KeyedInfo, new() {
        protected virtual string Key(string arg) {
            return arg;
        }

        protected override bool Workable(Context context) {
            if (null == context) throw new ArgumentNullException(nameof(context));
            if (context.HasProvider(out var provider)) {
                if (context.HasParameter(out var parameter)) {
                    var args = parameter.List;
                    if (args.Count > 0) {
                        foreach (var arg in args) {
                            var key = Key(arg);
                            var lookup = provider.DataKeyLookup(key);
                            if (lookup == null) {
                                return false;
                            }
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        protected override async IAsyncEnumerable<ICommandSuggestion> SuggestAsync(Context context, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (null == context) throw new ArgumentNullException(nameof(context));
            if (Triggered(context)) {
                await foreach (var suggestion in base.SuggestAsync(context, cancellationToken)) {
                    if (context.HasProvider(out var provider)) {
                        var keyAlias = provider.DataKeyAlias();
                        var keyItems = keyAlias.AllKeys
                            .Select(key => new { Key = key, Values = keyAlias.GetValues(key) })
                            .Select(item => new { item.Key, item.Values, First = item.Values.FirstOrDefault(), Others = item.Values.Skip(1) });
                        var paramExists = context.HasInfo(out var info) && !string.IsNullOrWhiteSpace(info.Parameter);
                        var suggestedItems = keyItems
                            .Where(item => paramExists
                                ? item.Values.Any(v => v.Contains(info.Parameter, StringComparison.CurrentCultureIgnoreCase))
                                : true)
                            .OrderBy(item => item.First ?? item.Key);
                        foreach (var item in suggestedItems) {
                            var key = item.Key;
                            var first = item.First;
                            var others = item.Others;
                            var input = suggestion.Input;
                            var help = input;
                            if (first == null) {
                                input += $" {key}";
                                help = input;
                            }
                            else {
                                input += $" {first}";
                                help = input;
                                if (others.Any()) {
                                    help += $" ({string.Join(',', others)})";
                                }
                            }
                            yield return Suggestion(
                                context: context,
                                help: help,
                                input: input,
                                relevance: suggestion.Relevance);
                        }
                    }
                }
            }
            else {
                await foreach (var suggestion in base.SuggestAsync(context, cancellationToken)) {
                    yield return suggestion;
                }
            }
        }

        public class KeyedInfo {
            [Argument(Name = "keys", Aggregate = true)]
            public string Keys { get; set; }

            public IReadOnlyList<string> List =>
                _List ?? (
                _List = Keys?.Split() ?? Array.Empty<string>());
            private IReadOnlyList<string> _List;
        }
    }
}
