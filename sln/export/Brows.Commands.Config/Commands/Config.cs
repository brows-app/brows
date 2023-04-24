namespace Brows.Commands {
    using CONFIGPATH = Brows.Config.ConfigPath;

    internal sealed class Config : Command<Config.Parameter> {
        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.GetParameter(out var parameter) == false) return false;
            return context.Operate(async (progress, token) => {
                var work = false;
                var open = parameter.Open;
                if (open.HasValue) {
                    var path = await CONFIGPATH.FileReady(token);
                    work |= await context.Provide(path, open.Value, token);
                }
                if (work == false) {
                    var path = await CONFIGPATH.FileReady(token);
                    work |= await context.Provide(path, CommandContextProvide.ActivePanel, token);
                }
                return work;
            });
        }

        public class Parameter {
            public CommandContextProvide? Open { get; set; }
        }
    }
}
