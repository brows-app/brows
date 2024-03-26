using Domore.Logs;
using Domore.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.ComponentModel.Composition.Registration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition {
    internal sealed class Host {
        private static readonly ILog Log = Logging.For(typeof(Host));

        private TaskCache<IReadOnlyList<Export>> LoadCache => _LoadCache ??=
            new TaskCache<IReadOnlyList<Export>>(LoadWork);
        private TaskCache<IReadOnlyList<Export>> _LoadCache;

        private string BaseDirectory => _BaseDirectory ??=
            AppContext.BaseDirectory;
        private string _BaseDirectory;

        private string ExportDirectory => _ExportDirectory ??=
            Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath),
                "brows.export");
        private string _ExportDirectory;

        private IReadOnlyList<Export> ExportFrom(Func<ReflectionContext, ComposablePartCatalog> factory) {
            if (null == factory) throw new ArgumentNullException(nameof(factory));
            var
            builder = new RegistrationBuilder();
            builder
                .ForTypesMatching(type => Export.Match(type, out _))
                .Export(builder => builder
                    .Inherited()
                    .AsContractType<IExport>());
            using (var catalog = factory(builder)) {
                if (Log.Info()) {
                    Log.Info(new object[] { catalog }.Concat(catalog).ToArray());
                }
                if (catalog is DirectoryCatalog directoryCatalog) {
                    if (Log.Info()) {
                        Log.Info(new object[] { nameof(DirectoryCatalog) }
                            .Append(nameof(directoryCatalog.FullPath) + " > " + directoryCatalog.FullPath)
                            .Append(nameof(directoryCatalog.SearchPattern) + " > " + directoryCatalog.SearchPattern)
                            .Append(nameof(directoryCatalog.LoadedFiles))
                            .Concat(directoryCatalog.LoadedFiles)
                            .ToArray());
                    }
                }
                using (var container = new CompositionContainer(catalog)) {
                    return container
                        .GetExportedValues<IExport>()
                        .Select(value => new Export(value.GetType(), file: null, instance: value))
                        .ToList();
                }
            }
        }

        private async Task<IReadOnlyList<Export>> LoadWork(CancellationToken token) {
            var applicationExportTask = Task.Run(cancellationToken: token, function: () => {
                return ExportFrom(builder => new ApplicationCatalog(builder));
            });
            var directoryExportTask = Task.Run(cancellationToken: token, function: () => {
                return ExportFrom(builder => new DirectoryCatalog(ExportDirectory, builder));
            });
            var exports = await Task.WhenAll(applicationExportTask, directoryExportTask);
            return exports.SelectMany(e => e).ToList();
        }

        public static readonly Host Current = new();

        public async Task<IReadOnlyList<Export>> Load(CancellationToken token) {
            return await LoadCache.Ready(token);
        }
    }
}
