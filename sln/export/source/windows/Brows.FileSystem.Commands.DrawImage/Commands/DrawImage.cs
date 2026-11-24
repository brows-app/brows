using Brows.Threading;
using Brows.Win32;
using Domore.Conf.Cli;
using Domore.Conf.Converters;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Commands;

internal sealed class DrawImage : FileSystemCommand<DrawImage.Parameter> {
    private readonly STAThreadPool ThreadPool = new(nameof(DrawImage)) { WorkerCountMin = 0 };

    [ImportOptional]
    internal IWin32InteropServices Interop { get; set; }

    protected sealed override IEnumerable<Type> Source { get; } = new[] {
        typeof(IEntry)
    };

    protected sealed override bool Work(Context context) {
        if (null == context) return false;
        if (false == context.HasPanel(out var active)) return false;
        if (false == context.HasParameter(out var parameter)) return false;
        if (false == active.HasFileSystemRename(out var rename)) {
            return false;
        }
        var outputs = rename.Expand(parameter.Output);
        var interop = Interop;
        var pathsAreEquivalent = interop is null
            ? default(Func<string, string, CancellationToken, Task<bool>>)
            : interop.PathsAreEquivalent;
        return context.Operate(async (progress, token) => {
            var work = new DrawImageWork(parameter, outputs, pathsAreEquivalent, ThreadPool);
            return await work.Run(progress, token);
        });
    }

    public sealed class Parameter {
        internal void Validate() {
            var rotateIsInvalid = Rotate is int rotate && rotate % 90 != 0;
            if (rotateIsInvalid) {
                throw new ArgumentOutOfRangeException(nameof(Rotate), Rotate, "Rotate must be a multiple of 90.");
            }
            if (Width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(Width), Width, "Width must be greater than zero.");
            }
            if (Height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(Height), Height, "Height must be greater than zero.");
            }
        }

        [CliArgument]
        public string Output { get; set; }

        public int? Width { get; set; }
        public int? Height { get; set; }
        public bool? Overwrite { get; set; }
        public int? Rotate { get; set; }
        public int? JpegQuality { get; set; }

        [CliDisplay(false)]
        public bool? CopyMetadata { get; set; }

        [CliDisplay(false)]
        [ConfListItems(Separator = ";")]
        public List<string> TransferMetadata {
            get => field ??= new();
            set;
        }
    }
}
