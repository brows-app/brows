using Domore.Conf.Cli;
using DRIVES = Brows.Drives;

namespace Brows.Commands {
    internal sealed class Drives : Command<Drives.Parameter> {
        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.GetParameter(out var parameter) == false) return false;
            return context.Operate(async (progress, token) => {
                return await context.Provide(DRIVES.ID, parameter.Where, token);
            });
        }

        public sealed class Parameter {
            [CliArgument]
            public CommandContextProvide Where { get; set; }
        }
    }
}
