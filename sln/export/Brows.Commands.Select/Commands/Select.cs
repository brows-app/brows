namespace Brows.Commands {
    internal sealed class Select : Command<SelectParameter> {
        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == context.HasParameter(out var parameter)) return false;
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
}
