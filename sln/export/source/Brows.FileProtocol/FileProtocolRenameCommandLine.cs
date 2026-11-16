using System.Text;

namespace Brows;

internal static class FileProtocolRenameCommandLine {
    internal static bool TryParseArgument(string parameter, out string name) {
        name = null;
        if (string.IsNullOrEmpty(parameter)) {
            return false;
        }

        /*
         * Domore.Conf.Cli concatenates adjacent quoted sections and treats backslashes literally.
         * Parse that positional form directly so whitespace inside the quotes is preserved.
         */
        var value = new StringBuilder();
        var quote = '\0';
        var argumentStarted = false;
        var argumentEnded = false;
        var hasNamedValue = false;
        foreach (var character in parameter) {
            if (quote != '\0') {
                if (character == quote) {
                    quote = '\0';
                }
                else {
                    value.Append(character);
                }
                continue;
            }

            var characterIsQuote = character is '\'' or '"';
            if (characterIsQuote) {
                if (argumentEnded) {
                    return false;
                }
                quote = character;
                argumentStarted = true;
                continue;
            }
            if (char.IsWhiteSpace(character)) {
                if (argumentStarted) {
                    argumentEnded = true;
                }
                continue;
            }
            if (argumentEnded) {
                return false;
            }
            if (character == '=') {
                hasNamedValue = true;
            }
            argumentStarted = true;
            value.Append(character);
        }

        var argumentIsInvalid = quote != '\0' || !argumentStarted || hasNamedValue;
        if (argumentIsInvalid) {
            return false;
        }
        name = value.ToString();
        return true;
    }

    internal static string QuoteArgument(string name, int selectedLength, out int selectedLengthInInput) {
        var quote = name.Contains('"') ? '\'' : '"';
        var otherQuote = quote == '"' ? '\'' : '"';
        var value = new StringBuilder(name.Length + 2);
        value.Append(quote);
        var selectedStartInInput = value.Length;
        selectedLengthInInput = 0;
        for (var i = 0; i < name.Length; i++) {
            var character = name[i];
            if (character == quote) {
                value.Append(quote);
                value.Append(otherQuote);
                value.Append(character);
                value.Append(otherQuote);
                value.Append(quote);
            }
            else {
                value.Append(character);
            }
            if (i + 1 == selectedLength) {
                selectedLengthInInput = value.Length - selectedStartInInput;
            }
        }
        value.Append(quote);
        return value.ToString();
    }

    internal static int SelectedLength(string name, FileProtocolEntryKind kind) {
        var entryIsDirectory = kind is FileProtocolEntryKind.Directory or FileProtocolEntryKind.DirectoryLink;
        var entryIsDotFile = name.StartsWith(".", StringComparison.Ordinal);
        var selectWholeName = entryIsDirectory || entryIsDotFile;
        if (selectWholeName) {
            return name.Length;
        }
        var extensionStart = name.LastIndexOf('.');
        return extensionStart > 0 ? extensionStart : name.Length;
    }
}
