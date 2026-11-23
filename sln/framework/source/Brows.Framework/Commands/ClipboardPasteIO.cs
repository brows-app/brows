using Brows.Composition;
using Domore.Logs;
using System.Collections.Generic;

namespace Brows.Commands;

/// <summary>
/// Provides a command that pastes clipboard sources into the active provider.
/// </summary>
public abstract class ClipboardPasteIO : Command<ClipboardPasteIO.Parameter> {
    private static readonly ILog Log = Logging.For(typeof(ClipboardPasteIO));

    /// <summary>
    /// Schedules a paste of the clipboard sources into the active provider. A move requested by the clipboard data
    /// is performed as a copy when the provider cannot move.
    /// </summary>
    /// <param name="context">
    /// The context supplying the destination provider.
    /// </param>
    /// <returns>
    /// True if the paste was scheduled; otherwise, false.
    /// </returns>
    protected sealed override bool Work(Context context) {
        if (context == null) return false;
        if (context.HasPanel(out var active) == false) {
            return false;
        }
        var hasCopyService = active.HasProviderService(out var copyProvider, out ICopyProvidedIO copyService);
        var hasMoveService = active.HasProviderService(out var moveProvider, out IMoveProvidedIO moveService);
        if (hasMoveService == false && hasCopyService == false) {
            return false;
        }
        var clipboard = Clipboard;
        if (clipboard == null) {
            return false;
        }
        return context.Operate(async (progress, token) => {
            var io = new List<IProvidedIO>();
            var data = new ClipboardData();
            var clipboardWork = await clipboard.Work(io, data, progress, token);
            if (clipboardWork == false) {
                return false;
            }
            if (data.MoveOnPaste) {
                if (moveService != null) {
                    var task = moveService.Work(io, moveProvider, progress, token);
                    if (task != null) {
                        return await task;
                    }
                    return false;
                }
                if (Log.Info()) {
                    Log.Info("Move service unavailable. Attempting to copy...");
                }
            }
            if (copyService != null) {
                var task = copyService.Work(io, copyProvider, progress, token);
                if (task != null) {
                    return await task;
                }
                return false;
            }
            return false;
        });
    }

    /// <summary>
    /// Gets or sets the service that reads clipboard sources.
    /// </summary>
    public IClipboardGetIO Clipboard { get; set; }

    /// <summary>
    /// Represents the parameters for the clipboard paste command.
    /// </summary>
    public sealed class Parameter {
    }
}
