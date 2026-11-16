using Domore.Conf.Cli;

namespace Brows.Commands;

internal sealed class IncludeIgnored : Command<IncludeIgnored.Parameter> {
    protected sealed override Type Provider => typeof(GitStatusProvider);

    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (!context.GetParameter(out var parameter)) {
            return false;
        }
        if (!context.HasPanel(out var panel)) {
            return false;
        }
        if (!panel.HasProvider<GitStatusProvider>(out var provider)) {
            return false;
        }
        provider.ShowIgnored = parameter.Value ?? !provider.ShowIgnored;
        return true;
    }

    public sealed class Parameter {
        [CliArgument]
        public bool? Value { get; set; }
    }
}
