using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Windows {
    internal sealed class AppExports {
        public IImport Import =>
            _Import ?? (
            _Import = Task.Run(async () => await Imports.Ready(CancellationToken.None)).Result);
        private IImport _Import;

        public AppComponentCollection Components =>
            _Components ?? (
            _Components = AppComponentCollection.From(Import.List<IExportResource>()));
        private AppComponentCollection _Components;

        public WindowsApplication App { get; }

        public AppExports(WindowsApplication app) {
            App = app ?? throw new ArgumentNullException(nameof(app));
        }
    }
}
