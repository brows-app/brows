using System;
using System.Collections.Generic;
using System.Text;

namespace Brows.Shared.Archives;

internal static class ArchiveProviderIDCodec {
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static bool HasArchiveProviderPrefix(string value) {
        if (value is null) {
            return false;
        }

        return value.StartsWith("zip:", StringComparison.Ordinal)
            || value.StartsWith("7z:", StringComparison.Ordinal)
            || value.StartsWith("tar:", StringComparison.Ordinal);
    }

    internal static bool TryParse(string value, out ArchiveProviderID archiveProviderID) {
        archiveProviderID = null;
        if (string.IsNullOrEmpty(value)) {
            return false;
        }

        try {
            archiveProviderID = Parse(value);
            return true;
        }
        catch (FormatException) {
            return false;
        }
        catch (DecoderFallbackException) {
            return false;
        }
    }

    internal static ArchiveProviderID Parse(string value) {
        if (value is null) {
            throw new ArgumentNullException(nameof(value));
        }

        var fields = value.Split('|', StringSplitOptions.None);
        var hasMinimumFieldCount = fields.Length >= 3;
        if (hasMinimumFieldCount == false) {
            throw new FormatException("The archive provider ID is incomplete.");
        }

        var format = ParseFormat(fields[0]);
        var firstFieldIsFile = fields[1].StartsWith("f=", StringComparison.Ordinal);
        var lastFieldIsDirectory = fields[^1].StartsWith("d=", StringComparison.Ordinal);
        if (firstFieldIsFile == false || lastFieldIsDirectory == false) {
            throw new FormatException("The archive provider ID has invalid field ordering.");
        }

        var fileName = Decode(fields[1][2..]);
        if (fileName.Length == 0) {
            throw new FormatException("The archive file name cannot be empty.");
        }

        var nestedMembers = new List<string>(fields.Length - 3);
        for (var i = 2; i < fields.Length - 1; i++) {
            var field = fields[i];
            var fieldIsNestedMember = field.StartsWith("n=", StringComparison.Ordinal);
            if (fieldIsNestedMember == false) {
                throw new FormatException("The archive provider ID has an unknown or misplaced field.");
            }

            var nestedMember = Decode(field[2..]);
            if (nestedMember.Length == 0) {
                throw new FormatException("Nested archive member names cannot be empty.");
            }
            nestedMembers.Add(nestedMember);
        }

        var directory = Decode(fields[^1][2..]);
        return new ArchiveProviderID(format, fileName, nestedMembers, directory);
    }

    internal static string Format(ArchiveProviderID archiveProviderID) {
        if (archiveProviderID is null) {
            throw new ArgumentNullException(nameof(archiveProviderID));
        }

        var builder = new StringBuilder();
        builder.Append(GetPrefix(archiveProviderID.Format));
        builder.Append("|f=");
        builder.Append(Encode(archiveProviderID.FileName));
        foreach (var nestedMember in archiveProviderID.NestedMembers) {
            builder.Append("|n=");
            builder.Append(Encode(nestedMember));
        }
        builder.Append("|d=");
        builder.Append(Encode(archiveProviderID.Directory));
        return builder.ToString();
    }

    private static string Encode(string value) {
        var containsNull = value.IndexOf('\0') >= 0;
        if (containsNull) {
            throw new ArgumentException("The archive provider ID cannot contain NUL.", nameof(value));
        }

        var bytes = StrictUtf8.GetBytes(value);
        var builder = new StringBuilder(bytes.Length);
        foreach (var currentByte in bytes) {
            if (IsSafe(currentByte)) {
                builder.Append((char)currentByte);
            }
            else {
                builder.Append('%');
                builder.Append(currentByte.ToString("X2", CultureInfo.InvariantCulture));
            }
        }
        return builder.ToString();
    }

    private static string Decode(string value) {
        var bytes = new List<byte>(value.Length);
        for (var i = 0; i < value.Length; i++) {
            var currentCharacter = value[i];
            if (currentCharacter == '%') {
                var escapeHasTwoDigits = i + 2 < value.Length;
                if (escapeHasTwoDigits == false) {
                    throw new FormatException("The archive provider ID has an incomplete escape.");
                }

                var highDigit = HexValue(value[i + 1]);
                var lowDigit = HexValue(value[i + 2]);
                var escapeHasHexDigits = highDigit >= 0 && lowDigit >= 0;
                if (escapeHasHexDigits == false) {
                    throw new FormatException("The archive provider ID has an invalid escape.");
                }

                bytes.Add((byte)((highDigit << 4) | lowDigit));
                i += 2;
                continue;
            }

            var characterIsSafe = currentCharacter <= 0x7f && IsSafe((byte)currentCharacter);
            if (characterIsSafe == false) {
                throw new FormatException("The archive provider ID contains an unescaped character.");
            }

            bytes.Add((byte)currentCharacter);
        }

        string decoded;
        try {
            decoded = StrictUtf8.GetString(bytes.ToArray());
        }
        catch (DecoderFallbackException exception) {
            throw new FormatException("The archive provider ID contains invalid UTF-8.", exception);
        }

        var containsNull = decoded.IndexOf('\0') >= 0;
        if (containsNull) {
            throw new FormatException("The archive provider ID cannot contain NUL.");
        }
        return decoded;
    }

    private static int HexValue(char value) {
        if (value >= '0' && value <= '9') {
            return value - '0';
        }
        if (value >= 'a' && value <= 'f') {
            return value - 'a' + 10;
        }
        if (value >= 'A' && value <= 'F') {
            return value - 'A' + 10;
        }
        return -1;
    }

    private static bool IsSafe(byte value) {
        var isLetterOrDigit = value >= 'A' && value <= 'Z'
            || value >= 'a' && value <= 'z'
            || value >= '0' && value <= '9';
        if (isLetterOrDigit) {
            return true;
        }

        return value == '-'
            || value == '.'
            || value == '_'
            || value == '~'
            || value == '/'
            || value == '\\'
            || value == ':';
    }

    private static ArchiveProviderIDFormat ParseFormat(string value) {
        if (string.Equals(value, "zip:1", StringComparison.Ordinal)) {
            return ArchiveProviderIDFormat.Zip;
        }
        if (string.Equals(value, "7z:1", StringComparison.Ordinal)) {
            return ArchiveProviderIDFormat.SevenZip;
        }
        if (string.Equals(value, "tar:1", StringComparison.Ordinal)) {
            return ArchiveProviderIDFormat.Tar;
        }
        throw new FormatException("The archive provider ID has an unknown format or version.");
    }

    private static string GetPrefix(ArchiveProviderIDFormat format) {
        return format switch {
            ArchiveProviderIDFormat.Zip => "zip:1",
            ArchiveProviderIDFormat.SevenZip => "7z:1",
            ArchiveProviderIDFormat.Tar => "tar:1",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown archive ID format.")
        };
    }
}
