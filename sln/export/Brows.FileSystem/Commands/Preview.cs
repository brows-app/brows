using Brows.Detail;

namespace Brows.Commands {
    internal sealed class Preview : FileSystemCommand<Preview.Parameter> {
        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) return false;
            if (active.HasProvider(out FileSystemProvider provider) == false) {
                return false;
            }
            if (provider.Detail is null) {
                provider.Detail = new FileSystemEntryPreview { Provider = provider };
                return true;
            }
            if (provider.Detail is FileSystemEntryPreview) {
                provider.Detail = null;
                return true;
            }
            return false;
        }

        public sealed class Parameter {
        }
    }
}
