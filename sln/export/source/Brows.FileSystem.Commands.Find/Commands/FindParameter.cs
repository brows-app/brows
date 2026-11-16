using Domore.Conf;
using Domore.Conf.Cli;
using Domore.Conf.Converters;
using Domore.Text;
using System;
using System.Collections.Generic;

namespace Brows.Commands;

internal sealed record FindParameter {
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
