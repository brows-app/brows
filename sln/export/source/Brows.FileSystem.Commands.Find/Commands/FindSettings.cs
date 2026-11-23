using Domore.Buffers;
using Domore.Text;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Commands;

internal sealed record FindSettings {
    private readonly DecodedTextOptions Decoder;

    private static BufferOptions Copy(BufferOptions options) {
        return new BufferOptions {
            Size = options.Size,
            Clear = options.Clear,
            Shared = options.Shared
        };
    }

    private static DecodedTextOptions Copy(DecodedTextOptions options) {
        return new DecodedTextOptions {
            Encoding = new List<string>(options.Encoding),
            EncodingFallback = new Dictionary<string, string>(
                options.EncodingFallback, options.EncodingFallback.Comparer),
            StreamBuffer = Copy(options.StreamBuffer),
            TextBuffer = Copy(options.TextBuffer)
        };
    }

    internal FindIn In { get; }
    internal FindMatcher Matcher { get; }
    internal IReadOnlyList<IMatcher> Include { get; }
    internal IReadOnlyList<IMatcher> Exclude { get; }
    internal int Workers { get; }
    internal long MaximumContentBytes { get; }
    internal int MaximumMatchesPerFile { get; }
    internal int MaximumResults { get; }
    internal int MaximumPreviewLength { get; }

    internal FindSettings(Find.Parameter parameter) : this(parameter, null) {
    }

    internal FindSettings(Find.Parameter parameter, FindMatcher matcher) {
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentOutOfRangeException.ThrowIfLessThan(parameter.Workers, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(parameter.Workers, 32);
        ArgumentOutOfRangeException.ThrowIfLessThan(parameter.MaximumContentBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(parameter.MaximumMatchesPerFile, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(parameter.MaximumResults, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(parameter.MaximumPreviewLength, 1);
        Workers = parameter.Workers;
        In = parameter.In;
        MaximumContentBytes = parameter.MaximumContentBytes;
        MaximumMatchesPerFile = parameter.MaximumMatchesPerFile;
        MaximumResults = parameter.MaximumResults;
        MaximumPreviewLength = parameter.MaximumPreviewLength;
        Matcher = matcher ?? parameter.PatternMatcher();
        Include = parameter.IncludeFilter().Matchers().ToArray();
        Exclude = parameter.ExcludeFilter().Matchers().ToArray();
        Decoder = Copy(parameter.Decoder);
    }

    internal DecodedTextOptions CreateDecoder() {
        return Copy(Decoder);
    }
}
