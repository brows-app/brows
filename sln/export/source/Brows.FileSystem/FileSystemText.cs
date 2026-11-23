using Brows.Localization;
using System;

namespace Brows;

internal static class FileSystemText {
    internal static string Value(ITranslation translation, string key) {
        if (string.IsNullOrEmpty(key)) {
            return string.Empty;
        }
        if (translation is null) {
            return key;
        }
        var value = translation.Value(key);
        return string.IsNullOrEmpty(value) ? key : value;
    }
}