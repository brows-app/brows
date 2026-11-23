using Domore.Conf;
using Domore.Conf.Cli;
using Domore.Conf.Converters;
using Domore.Text;
using System;
using System.Collections.Generic;

namespace Brows.Commands;

internal sealed class Find : FileSystemCommand<Find.Parameter> {
    private FileSystemFindCommand FindCommand => field ??= new FileSystemFindCommand();

    protected sealed override IEnumerable<Type> Source { get; } = new[] {
        typeof(IEntry),
        typeof(IEntryObservation)
    };

    protected sealed override IAsyncEnumerable<ICommandSuggestion> Suggest(Context context, CancellationToken token) {
        if (context != null) {
            if (context.DidTrigger(this)) {
                if (context.HasData(out _) == false) {
                    var dataList = FindCommand.List;
                    if (dataList.Count > 0) {
                        var data = new FileSystemFindData(this, context, 0, dataList);
                        context.SetData(data);
                        context.SetHint(data);
                    }
                }
            }
        }
        return base.Suggest(context, token);
    }

    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (context.HasGesture(out _)) {
            if (context.HasCommander(out var commander)) {
                return context.ShowPalette($"{InputTrigger} ");
            }
        }
        if (context.HasInput(out var input) == false) {
            return false;
        }
        if (context.HasParameter(out var parameter) == false) {
            return false;
        }
        if (context.HasPanel(out var active) == false) {
            return false;
        }
        if (active.HasFileSystemDirectory(out var directory) == false) {
            return false;
        }
        if (string.IsNullOrWhiteSpace(parameter.Pattern)) {
            return false;
        }
        var conf = context.HasConf(out var c) ? c.Text : null;
        var work = new FindWork(new FileSystemFindResult(directory, parameter, input, conf) {
            Translation = Translation
        });
        var list = FindCommand.List;
        list.Insert(0, work.Result);
        FindCommand.PruneCompleted();
        var data = new FileSystemFindData(this, context, 0, list);
        context.SetData(data);
        context.SetHint(data);
        context.SetFlag(new CommandContextFlag {
            PersistInput = true,
            RefreshInput = false
        });
        return context.Operate(async (progress, token) => {
            try {
                await work.Done(progress, token);
                return true;
            }
            finally {
                FindCommand.PruneCompleted();
            }
        });
    }

    public ITranslationExport Translation { get; set; }

    public sealed record Parameter {
        [CliArgument]
        [CliRequired]
        public string Pattern { get; set; }
        public FindIn In { get; set; } = FindIn.FileName;
        public FindCaseSensitivity Case { get; set; }

        [ConfListItems(Separator = ";")]
        public List<string> Exclude {
            get => field ??= new();
            set;
        }

        [Conf("exclude-case")]
        public FindCaseSensitivity ExcludeCase { get; set; }

        [ConfListItems(Separator = ";")]
        public List<string> Include {
            get => field ??= new();
            set;
        }

        [Conf("include-case")]
        public FindCaseSensitivity IncludeCase { get; set; }

        [CliDisplay(false)]
        public DecodedTextOptions Decoder {
            get => field ??= new();
            set;
        }

        public int Workers { get; set; } = Math.Max(1, Math.Min(4, Environment.ProcessorCount));

        [Conf("max-content-bytes")]
        public long MaximumContentBytes { get; set; } = 8 * 1024 * 1024;

        [Conf("max-matches-per-file")]
        public int MaximumMatchesPerFile { get; set; } = 256;

        [Conf("max-results")]
        public int MaximumResults { get; set; } = 10000;

        [Conf("max-preview-length")]
        public int MaximumPreviewLength { get; set; } = 512;

        public FindMatcher PatternMatcher() {
            return new FindMatcher(Pattern, ignoreCase: Case == FindCaseSensitivity.None);
        }

        public FindFilter ExcludeFilter() {
            return new FindFilter { Case = ExcludeCase, List = Exclude };
        }

        public FindFilter IncludeFilter() {
            return new FindFilter { Case = IncludeCase, List = Include };
        }
    }
}
