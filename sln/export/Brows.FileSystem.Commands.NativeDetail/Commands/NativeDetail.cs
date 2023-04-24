namespace Brows.Commands {
    using Exports;

    internal sealed class NativeDetail : FileSystemCommand<NativeDetail.Parameter> {
        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) return false;
            if (active.HasFileSystemSelection(out var selection) == false) return false;
            var service = DetailFileSystemInfos;
            if (service == null) {
                return false;
            }
            return context.Operate(async (progress, cancellationToken) => {
                return await service.Work(selection, progress, cancellationToken);
            });
        }

        public IDetailFileSystemInfos DetailFileSystemInfos { get; set; }

        public sealed class Parameter {
        }
    }
}
