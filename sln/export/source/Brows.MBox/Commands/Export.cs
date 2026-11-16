using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

/// <summary>
/// Extracts the selected messages of a mailbox to files of an Internet message.
/// </summary>
internal sealed class Export : MBoxCommand<ExportParameter> {
    internal MBoxExportPublisher Publisher { get; set; } = new();

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
        if (active.HasProvider(out MBoxProvider provider) == false) {
            return false;
        }
        var destination = parameter.Destination;
        if (string.IsNullOrWhiteSpace(destination)) {
            return false;
        }
        var full = Path.GetFullPath(destination);
        if (SamePath(provider.Path, full)) {
            /*
             * The mailbox itself is never a destination, because the source is read only.
             */
            return false;
        }
        var directory = Directory.Exists(full) ? new DirectoryInfo(full) : null;
        var file = directory == null ? new FileInfo(full) : null;
        var severalMessagesNeedDirectory = entries.Count > 1 && directory == null;
        if (severalMessagesNeedDirectory) {
            /*
             * Several messages need a directory to be written to.
             */
            return false;
        }
        return context.Operate(async (progress, token) => {
            var destinations = Destinations(entries, directory, file);
            var duplicates = destinations
                .GroupBy(pair => pair.Destination, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
            if (duplicates.Count > 0) {
                return false;
            }
            var worked = false;
            foreach (var pair in destinations) {
                if (token.IsCancellationRequested) {
                    break;
                }
                var exported = await ExportMessage(
                    pair.Entry,
                    pair.Destination,
                    parameter.Overwrite,
                    Publisher,
                    token);
                worked |= exported;
            }
            return worked;
        });
    }

    private static List<(MBoxEntry Entry, string Destination)> Destinations(IReadOnlyList<MBoxEntry> entries,
                                                                           DirectoryInfo directory,
                                                                           FileInfo file) {
        var list = new List<(MBoxEntry, string)>();
        foreach (var entry in entries) {
            if (directory != null) {
                list.Add((entry, Path.Combine(directory.FullName, MBoxPath.MessageName(entry.Record.Offset))));
            }
            else {
                list.Add((entry, file.FullName));
            }
        }
        return list;
    }

    private static bool SamePath(string first, string second) {
        return string.Equals(
            Path.GetFullPath(first),
            Path.GetFullPath(second),
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> ExportMessage(MBoxEntry entry,
                                                  string destination,
                                                  bool overwrite,
                                                  MBoxExportPublisher publisher,
                                                  CancellationToken token) {
        if (entry.Session.IsRetired) {
            return false;
        }
        var destinationExists = File.Exists(destination);
        var overwriteIsDisabled = overwrite == false;
        var shouldDeclineExistingDestination = destinationExists && overwriteIsDisabled;
        if (shouldDeclineExistingDestination) {
            return false;
        }
        var directory = Path.GetDirectoryName(destination);
        if (string.IsNullOrEmpty(directory) == false) {
            Directory.CreateDirectory(directory);
        }
        /*
         * The message is written to a sibling file of its own, so that a failure never leaves a
         * half-written file at the destination.
         */
        var staging = Path.Combine(
            directory ?? ".",
            $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try {
            await Task.Run(action: () => {
                MBoxExtractor.Extract(entry, staging, token);
            }, cancellationToken: token);
            token.ThrowIfCancellationRequested();
            publisher.Publish(staging, destination, overwrite);
            return true;
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception) {
            return false;
        }
        finally {
            TryRemove(staging);
        }
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
}
