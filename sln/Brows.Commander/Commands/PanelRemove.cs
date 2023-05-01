namespace Brows.Commands {
    internal sealed class PanelRemove : Command<PanelRemove.Parameter> {
        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == context.HasCommander(out var commander)) return false;
            if (false == context.GetParameter(out var parameter)) return false;
            return context.Operate(async (progress, token) => {
                if (parameter.All == true) {
                    return await commander.ClearPanels(token);
                }
                else {
                    return await commander.RemovePanel(active, token);
                }
            });
        }

        public sealed class Parameter {
            public bool? All { get; set; }
        }
    }
}
