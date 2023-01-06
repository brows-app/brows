using Domore.Logs;
using System;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO.Compression.Formats {
    using Threading.Tasks;

    internal class Zip : CompressionFormat {
        private static readonly ILog Log = Logging.For(typeof(Zip));

        private CompressionInput Input =>
            _Input ?? (
            _Input = new CompressionInput());
        private CompressionInput _Input;

        protected override string Extension =>
            "zip";

        protected override async Task Create(ICompressionCreate state, CancellationToken cancellationToken) {
            if (null == state) throw new ArgumentNullException(nameof(state));
            var output = state.Output;
            var progress = state.Progress;
            var level = state.Level;
            var info = progress.Info;
            var input = await Input.Files(progress, state.Entries, cancellationToken);
            var
            target = progress.Target;
            target.Add(input.Count);
            using (var zip = ZipFile.Open(output, ZipArchiveMode.Create)) {
                await foreach (var item in input.Files(cancellationToken)) {
                    var name = item.Name;
                    var file = item.File;
                    var path = file.FullName;
                    if (Log.Info()) {
                        Log.Info(nameof(ZipFileExtensions.CreateEntryFromFile) + " > " + name);
                    }
                    info.Data("Create {0}", name);
                    await Async.Run(cancellationToken, () => {
                        if (level.HasValue) {
                            zip.CreateEntryFromFile(path, name, level.Value);
                        }
                        else {
                            zip.CreateEntryFromFile(path, name);
                        }
                    });
                    progress.Add(1);
                }
            }
        }
    }
}
