using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal class Select : Command<Select.Parameter>, ICommandExport {
        //protected override IEnumerable<ITrigger> DefaultTriggers {
        //    get {
        //        yield return TriggerInput;
        //        //yield return TriggerSelectAll;
        //    }
        //}

        protected override async Task<bool> Work(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            //if (context.HasKey(out var gesture)) {
            //    if (gesture.Equals(TriggerSelectAll.Gesture)) {
            //        if (context.HasPanel(out var active)) {
            //            await active.Entries.ChangeSelection(cancellationToken, add: active.Entries);
            //        }
            //    }
            //}
            if (context.HasParameter(out var parameter)) {
                if (context.HasPanel(out var active)) {
                    var add = parameter.Add;
                    var all = parameter.All;
                    var entries = active.Entries;
                    foreach (var entry in entries.Items) {
                        entry.Selected = all || (add && entry.Selected);
                    }
                    var pattern = parameter.Pattern?.Trim() ?? "";
                    if (pattern != "") {
                        var matcher = MatchAlgorithm.Create(ignoreCase: !parameter.CaseSensitive);
                        var matches = matcher.Match(parameter.Pattern, entries.Items, entry => entry.Name, cancellationToken);
                        await foreach (var match in matches) {
                            match.Selected = true;
                        }
                    }
                }
                return true;
            }
            return false;
        }

        //public InputTrigger TriggerInput { get; set; } = new InputTrigger("select", "sel");
        //public KeyboardTrigger TriggerSelectAll { get; set; } = new KeyboardTrigger(KeyboardKey.A, KeyboardModifiers.Control);

        public class Parameter {
            [Argument(Name = "pattern", Order = 0, Required = true)]
            public string Pattern { get; set; }

            [Switch(Name = "add", ShortName = 'a')]
            public bool Add { get; set; }

            [Switch(Name = "all")]
            public bool All { get; set; }

            [Switch(Name = "case-sensitive")]
            public bool CaseSensitive { get; set; }
        }
    }
}
