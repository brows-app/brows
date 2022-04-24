using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class Select : Command<Select.Parameter>, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger("select", "sel");
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasParameter(out var parameter)) {
                if (context.HasPanel(out var active)) {
                    var add = parameter.Add;
                    var entries = active.Entries;
                    foreach (var entry in entries) {
                        entry.Selected = add && entry.Selected;
                    }
                    var pattern = parameter.Pattern?.Trim() ?? "";
                    if (pattern != "") {
                        var matcher = MatchAlgorithm.Create(ignoreCase: !parameter.CaseSensitive);
                        var matches = matcher.Match(parameter.Pattern, entries, entry => entry.Name, cancellationToken);
                        await foreach (var match in matches) {
                            match.Selected = true;
                        }
                    }
                }
                return true;
            }
            return false;
        }

        public class Parameter {
            [Argument(Name = "pattern", Order = 0, Required = true)]
            public string Pattern { get; set; }

            [Switch(Name = "add", ShortName = 'a')]
            public bool Add { get; set; }

            [Switch(Name = "case-sensitive")]
            public bool CaseSensitive { get; set; }
        }
    }
}
