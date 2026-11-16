using Brows.Detail;
using System;

namespace Brows.Commands;

internal sealed class View : SevenZipCommand<View.Parameter> {
    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (context.HasPanel(out var active) is false) {
            return false;
        }
        if (context.HasParameter(out var parameter) is false) {
            return false;
        }
        if (active.HasProvider(out SevenZipProvider provider) is false) {
            return false;
        }
        var toggle = parameter.Toggle;
        if (toggle.HasFlag(Views.Preview)) {
            return context.Operate(async (progress, token) => {
                return await provider.Detail.ToggleAsync(
                    Views.Preview, () => new SevenZipEntryPreview(provider), token);
            });
        }
        return true;
    }

    [Flags]
    internal enum Views {
        None = 0,
        Navigation = 1,
        Preview = 2,
    }

    public sealed record Parameter {
        public Views Toggle { get; init; }
    }
}
