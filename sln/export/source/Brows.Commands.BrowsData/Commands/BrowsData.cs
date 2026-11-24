namespace Brows.Commands;

internal sealed class BrowsData : Command<BrowsData.Parameter> {
    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (!context.GetParameter(out var parameter)) {
            return false;
        }
        return context.Operate(async (progress, token) => {
            var work = false;
            var open = parameter.Open;
            if (open.HasValue) {
                var path = await ConfigurationPath.DataReady(token);
                work |= await context.Provide(path, open.Value, token);
            }
            if (!work) {
                var path = await ConfigurationPath.DataReady(token);
                work |= await context.Provide(path, CommandContextProvide.ActivePanel, token);
            }
            return work;
        });
    }

    public sealed class Parameter {
        public CommandContextProvide? Open { get; set; }
    }
}
