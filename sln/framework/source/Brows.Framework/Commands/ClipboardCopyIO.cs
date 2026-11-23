using Brows.Composition;
using System.Collections.Generic;

namespace Brows.Commands;

/// <summary>
/// Provides a command that places source items on the clipboard for copying or moving.
/// </summary>
public abstract class ClipboardCopyIO : Command<ClipboardCopyIO.Parameter> {
    /// <summary>
    /// Schedules placement of the current source items on the clipboard.
    /// </summary>
    /// <param name="context">
    /// The context supplying the source and paste settings.
    /// </param>
    /// <returns>
    /// True if the operation was scheduled; otherwise, false.
    /// </returns>
    protected sealed override bool Work(Context context) {
        if (null == context) return false;
        if (false == context.HasSource(out var source)) return false;
        if (false == context.HasPanel(out var active)) return false;
        if (false == active.HasProviderService(out var provider, out IProvideIO service)) return false;
        if (false == context.GetParameter(out var parameter)) return false;
        return context.Operate(async (progress, token) => {
            var clipboard = Clipboard;
            if (clipboard == null) {
                return false;
            }
            var io = new List<IProvidedIO>();
            var ioWork = await service.Work(io, source, provider, progress, token);
            if (ioWork == false) {
                return false;
            }
            var data = new ClipboardData { MoveOnPaste = parameter.MoveOnPaste };
            var task = clipboard.Work(io, data, progress, token);
            if (task == null) {
                return false;
            }
            return await task;
        });
    }

    /// <summary>
    /// Gets or sets the service that places sources on the clipboard.
    /// </summary>
    public IClipboardSetIO Clipboard { get; set; }

    /// <summary>
    /// Specifies how clipboard items should be pasted.
    /// </summary>
    public sealed class Parameter {
        /// <summary>
        /// Gets or sets whether pasting should move the source items instead of copying them.
        /// </summary>
        public bool MoveOnPaste { get; set; }
    }
}
