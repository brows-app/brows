using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Brows;

/*
 * Names the file of an attachment. The name of an attachment is chosen by the sender, so it may
 * hold a path, characters that a file name cannot hold, or the name of a device.
 */
internal static class MBoxAttachmentName {
    private const string UnnamedFileName = "attachment";
    private const int MaxFileNameLength = 200;

    private static readonly HashSet<string> DeviceNames = new(StringComparer.OrdinalIgnoreCase) {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    /*
     * Returns a name that is safe to use as the name of a file.
     */
    internal static string FileName(string fileName) {
        var name = fileName ?? string.Empty;
        var separator = name.LastIndexOfAny(['/', '\\']);
        if (separator >= 0) {
            name = name[(separator + 1)..];
        }
        var characters = name
            .Select(character => {
                var characterIsInvalid = char.IsControl(character) || InvalidFileNameChars.Contains(character);
                return characterIsInvalid ? '_' : character;
            })
            .ToArray();
        name = new string(characters).Trim().TrimEnd('.', ' ');
        if (name.Length == 0) {
            return UnnamedFileName;
        }
        var stem = name.Split('.')[0].TrimEnd(' ');
        if (DeviceNames.Contains(stem)) {
            name = "_" + name;
        }
        if (name.Length > MaxFileNameLength) {
            var extension = Path.GetExtension(name);
            var extensionIsKept = extension.Length < MaxFileNameLength / 2;
            name = extensionIsKept
                ? name[..(MaxFileNameLength - extension.Length)] + extension
                : name[..MaxFileNameLength];
        }
        return name;
    }

    /*
     * Returns a safe name that is not one of the names already taken, so that two attachments
     * with the same name are both kept. A repeated name is numbered, such as "report (2).pdf".
     */
    internal static string Unique(string fileName, ISet<string> taken) {
        ArgumentNullException.ThrowIfNull(taken);
        var name = FileName(fileName);
        var stem = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        var unique = name;
        for (var number = 2; taken.Contains(unique); number++) {
            unique = $"{stem} ({number}){extension}";
        }
        return unique;
    }
}
