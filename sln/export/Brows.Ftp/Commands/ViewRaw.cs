namespace Brows.Commands {
    internal sealed class ViewRaw : FtpCommand<ViewRaw.Parameter> {
        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasPanel(out var panel)) return false;
            if (false == panel.HasProvider<FtpProvider>(out var provider)) {
                return false;
            }
            provider.ViewRaw = !provider.ViewRaw;
            return true;
        }

        public sealed class Parameter {
        }
    }
}
