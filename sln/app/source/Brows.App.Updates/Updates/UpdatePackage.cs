using System;

namespace Brows.Updates;

internal static class UpdatePackage {
    internal const long MaximumSize = 1L << 30;

    internal static string Name(string architecture, string version) {
        return $"brows-{architecture}-{version}.msi".ToLowerInvariant();
    }

    internal static bool IsSha256(ReadOnlySpan<char> value) {
        return value.Length == 64 && value.IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;
    }
}
