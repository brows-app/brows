using System.Collections.Generic;

namespace Brows.Commands {
    using Exports;

    public abstract class ClipboardPasteIO : Command<ClipboardPasteIO.Parameter> {
        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) {
                return false;
            }
            if (active.HasProviderService(out var provider, out ICopyProvidedIO service) == false) {
                return false;
            }
            var clipboard = Clipboard;
            if (clipboard == null) {
                return false;
            }
            return context.Operate(async (progress, token) => {
                var io = new List<IProvidedIO>();
                var clipboardWork = await Clipboard.Work(io, progress, token);
                if (clipboardWork == false) {
                    return false;
                }
                return await service.Work(io, provider, progress, token);
            });
        }

        public IClipboardGetIO Clipboard { get; set; }

        public class Parameter {
        }
    }
}
