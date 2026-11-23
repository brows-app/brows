using Domore.Conf.Cli;

namespace Brows.Commands;

internal sealed class Logs : Command<Logs.Parameter> {
    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (context.GetParameter(out var parameter) == false) {
            return false;
        }
        var where = parameter.Where ?? CommandContextProvide.AddPanel;
        return context.Operate(async (progress, token) => {
            return await context.Provide(LogsProviderFactory.ProviderID, where, token);
        });
    }

    public sealed class Parameter {
        [CliArgument]
        public CommandContextProvide? Where { get; set; }
    }
}
