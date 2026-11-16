using Brows.Detail;
using System;

namespace Brows.Commands;

internal sealed class View : FileSystemCommand<View.Parameter> {
    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (context.HasPanel(out var active) == false) {
            return false;
        }
        if (context.HasParameter(out var parameter) == false) {
            return false;
        }
        if (active.HasProvider(out FileSystemProvider provider) == false) {
            return false;
        }
        var toggle = parameter.Toggle;
        var worked = true;
        if (toggle.HasFlag(Views.Preview)) {
            worked = context.Operate(async (progress, token) => {
                return await provider.Detail.ToggleAsync(
                    Views.Preview, () => new FileSystemEntryPreview(provider), token);
            });
        }
        if (toggle.HasFlag(Views.Navigation)) {
            var tree = provider.Navigation = provider.Navigation is null
                ? new FileSystemTree(provider.Factory)
                : null;
            if (tree is not null) {
                tree.Provided(provider);
            }
        }
        return worked;
    }

    [Flags]
    public enum Views {
        None = 0,
        Navigation = 1,
        Preview = 2,
    }

    public sealed class Parameter {
        public Views Toggle { get; set; }
    }
}
