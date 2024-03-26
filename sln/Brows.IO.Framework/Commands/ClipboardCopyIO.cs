using Brows.Exports;
using System.Collections.Generic;

namespace Brows.Commands {
    public abstract class ClipboardCopyIO : Command<ClipboardCopyIO.Parameter> {
        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasSource(out var source)) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == active.HasProviderService(out var provider, out IProvideIO service)) return false;
            if (false == context.GetParameter(out var parameter)) return false;
            return context.Operate(async (progress, token) => {
                var clipboard = Clipboard;
                if (clipboard == null) {
                    return false;
                }
                var io = new List<IProvidedIO>();
                var ioWork = await service.Work(io, source, provider, progress, token);
                if (ioWork == false) {
                    return false;
                }
                var data = new ClipboardData { MoveOnPaste = parameter.MoveOnPaste };
                var task = clipboard.Work(io, data, progress, token);
                if (task == null) {
                    return false;
                }
                return await task;
            });
        }

        public IClipboardSetIO Clipboard { get; set; }

        public class Parameter {
            public bool MoveOnPaste { get; set; }
        }
    }
}
