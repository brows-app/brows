using Brows.Entries;
using Brows.Threading;
using Domore.Conf.Cli;
using Domore.Conf.Converters;
using System;
using System.Collections.Generic;

namespace Brows.Commands;

internal sealed class DrawImage : FileSystemCommand<DrawImage.Parameter> {
    private readonly STAThreadPool ThreadPool = new(nameof(DrawImage)) { WorkerCountMin = 0 };

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
        return context.Operate(async (progress, token) => {
            var work = new DrawImageWork(parameter, rename, ThreadPool);
            return await work.Run(progress, token);
        });
    }

    public sealed class Parameter {
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
