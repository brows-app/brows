using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

/// <summary>
/// Extracts the selected messages of a mailbox, and opens them with the associated application.
/// </summary>
internal sealed class Open : MBoxCommand<Open.Parameter> {
    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (context.HasPanel(out var active) == false) {
            return false;
        }
        if (context.GetParameter(out var parameter) == false) {
            return false;
        }
        if (active.HasMBoxSelection(out var entries) == false) {
            return false;
        }
        return context.Operate(async (progress, token) => {
            var worked = false;
            foreach (var entry in entries) {
                if (token.IsCancellationRequested) {
                    break;
                }
                var opened = await OpenMessage(entry, token);
                worked |= opened;
            }
            return worked;
        });
    }

    /// <summary>
    /// Gets or sets the service that opens a file with the associated application.
    /// </summary>
    public IOpenFile OpenFile { get; set; }

    internal string TempRoot { get; set; }

    private async Task<bool> OpenMessage(MBoxEntry entry, CancellationToken token) {
        var session = entry.Session;
        if (session.IsRetired) {
            /*
             * The message belongs to a generation that was already retired, so it is not opened.
             */
            return false;
        }
        var root = TempRoot ?? MBoxTemp.Root;
        MBoxTemp.SessionDirectory(session.GenerationId, root);
        var path = MBoxExtractor.TempPath(session.GenerationId, entry.Record.Offset, root);
        var created = false;
        try {
            await Task.Run(action: () => {
                MBoxTemp.Acquire(session.GenerationId, root);
            }, cancellationToken: token);
            if (File.Exists(path) == false) {
                await Task.Run(action: () => {
                    MBoxExtractor.Extract(entry, path, token);
                }, cancellationToken: token);
                created = true;
            }
        }
        catch (OperationCanceledException) {
            if (created) {
                TryRemove(path);
            }
            throw;
        }
        catch (Exception) {
            return false;
        }
        var service = OpenFile;
        if (service == null) {
            if (created) {
                TryRemove(path);
            }
            return false;
        }
        var opened = await service.Work(path, token);
        if (opened == false) {
            if (created) {
                TryRemove(path);
            }
            return false;
        }
        /*
         * The file stays until the application exits, because the viewer may read it later.
         */
        return true;
    }

    private static void TryRemove(string path) {
        try {
            File.Delete(path);
        }
        catch (IOException) {
        }
        catch (UnauthorizedAccessException) {
        }
    }

    /// <summary>
    /// Holds the parameters of the command.
    /// </summary>
    public sealed class Parameter {
    }
}
