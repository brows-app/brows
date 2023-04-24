namespace Brows.Commands {
    internal class Select : Command<Select.Parameter> {
        //protected override IEnumerable<ITrigger> DefaultTriggers {
        //    get {
        //        yield return TriggerInput;
        //        //yield return TriggerSelectAll;
        //    }
        //}

        protected sealed override bool Work(Context context) {
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
                    return context.Operate(async (progress, token) => {
                        var add = parameter.Add;
                        var all = parameter.All;
                        if (active.HasEntries(out var entries)) {
                            foreach (var entry in entries) {
                                entry.Select = all || (add && entry.Select);
                            }
                        }
                        var pattern = parameter.Pattern?.Trim() ?? "";
                        if (pattern != "") {
                            var matcher = MatchAlgorithm.Create(ignoreCase: !parameter.CaseSensitive);
                            var matches = matcher.Match(parameter.Pattern, entries, entry => entry.Name, token);
                            await foreach (var match in matches) {
                                match.Select = true;
                            }
                        }
                        return true;
                    });
                }
            }
            return false;
        }

        //public InputTrigger TriggerInput { get; set; } = new InputTrigger("select", "sel");
        //public KeyboardTrigger TriggerSelectAll { get; set; } = new KeyboardTrigger(KeyboardKey.A, KeyboardModifiers.Control);

        public class Parameter {
            public string Pattern { get; set; }
            public bool Add { get; set; }
            public bool All { get; set; }
            public bool CaseSensitive { get; set; }
        }
    }
}
