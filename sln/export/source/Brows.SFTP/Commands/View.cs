using Brows.Detail;
using System;

namespace Brows.Commands;

internal sealed class View : SftpCommand<View.Parameter> {
    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (!context.HasPanel(out var active)) {
            return false;
        }
        if (!context.GetParameter(out var parameter)) {
            return false;
        }
        if (!active.HasProvider<SftpProvider>(out var provider)) {
            return false;
        }
        if (parameter.Toggle.HasFlag(Views.Preview)) {
            return context.Operate(async (progress, token) => {
                return await provider.Detail.ToggleAsync(Views.Preview, () => new SftpEntryPreview(provider), token);
            });
        }
        return true;
    }

    [Flags]
    internal enum Views {
        None = 0,
        Preview = 2,
    }

    public sealed class Parameter {
        public Views Toggle { get; set; } = Views.Preview;
    }
}
