namespace Brows.Commands {
    internal sealed class Palette : Command<Palette.Parameter> {
        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.GetParameter(out var parameter)) {
                return false;
            }
            var show = parameter.Show;
            var input =
                show == Show.Nothing ? "" :
                show == Show.ProviderID && context.HasPanel(out var active) && active.HasProvider(out Provider provider) ? provider.ID :
                "";
            return context.ShowPalette(
                input: input,
                selectedStart: 0,
                selectedLength: input.Length);
        }

        public sealed class Parameter {
            public Show Show { get; set; }
        }

        public enum Show {
            Nothing = 0,
            ProviderID
        }
    }
}
