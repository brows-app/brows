using Brows.Localization;
using System;

namespace Brows;

internal static class FileSystemText {
    internal static string Value(string key) {
        if (string.IsNullOrEmpty(key)) {
            return string.Empty;
        }
        var translate = Translation.Global;
        if (translate is null) {
            return key;
        }
        var value = translate.Value(key);
        return string.IsNullOrEmpty(value) ? key : value;
    }
}