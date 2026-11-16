using Brows.Detail;
using System;

namespace Brows.Commands;

/// <summary>
/// Shows or hides the preview of the messages of a mailbox.
/// </summary>
internal sealed class View : MBoxCommand<View.Parameter> {
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
        if (active.HasProvider(out MBoxProvider provider) == false) {
            return false;
        }
        var toggle = parameter.Toggle;
        if (toggle.HasFlag(MBoxViews.Preview)) {
            return context.Operate(async (progress, token) => {
                return await provider.Detail.ToggleAsync(
                    MBoxViews.Preview, () => new MBoxEntryPreview(provider), token);
            });
        }
        return true;
    }

    /// <summary>
    /// Holds the parameters of the command.
    /// </summary>
    public sealed class Parameter {
        /// <summary>
        /// Gets or sets the views to toggle.
        /// </summary>
        public MBoxViews Toggle { get; set; }
    }
}
