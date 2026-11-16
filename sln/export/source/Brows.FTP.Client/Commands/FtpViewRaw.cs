namespace Brows.Commands;

internal abstract class FtpViewRaw<TProvider> : FtpCommand<TProvider, FtpViewRaw<TProvider>.Parameter>
where TProvider : FtpProviderBase {
    protected sealed override bool Work(Context context) {
        if (context is null || !context.HasPanel(out var panel) || !panel.HasProvider<TProvider>(out var provider)) {
            return false;
        }
        provider.ViewRaw = !provider.ViewRaw;
        return true;
    }

    public sealed class Parameter {
    }
}
