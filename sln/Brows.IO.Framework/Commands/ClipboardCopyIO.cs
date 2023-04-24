using System.Collections.Generic;

namespace Brows.Commands {
    using Exports;

    public abstract class ClipboardCopyIO : Command<ClipboardCopyIO.Parameter> {
        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) {
                return false;
            }
            if (active.HasProviderService(out var provider, out IProvideIO service) == false) {
                return false;
            }
            return context.Operate(async (progress, token) => {
                var io = new List<IProvidedIO>();
                var ioWork = await service.Work(io, provider, progress, token);
                if (ioWork == false) {
                    return false;
                }
                var clipboard = Clipboard;
                if (clipboard == null) {
                    return false;
                }
                return await clipboard.Work(io, progress, token);
            });
        }

        public IClipboardSetIO Clipboard { get; set; }

        public class Parameter {
        }
    }
}
