using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Brows;

internal static class AppArchitecture {
    private const string X64 = "x64";
    private const string Arm64 = "arm64";

    internal static IReadOnlyList<string> Tokens { get; } = [X64, Arm64];

    internal static string From(Architecture process) {
        return process switch {
            Architecture.X64 => X64,
            Architecture.Arm64 => Arm64,
            _ => null
        };
    }

    internal static bool TryNormalize(string value, out string token) {
        var trimmed = value?.Trim();
        foreach (var supported in Tokens) {
            if (string.Equals(supported, trimmed, StringComparison.OrdinalIgnoreCase)) {
                token = supported;
                return true;
            }
        }
        token = null;
        return false;
    }

    internal static bool Runs(string token, Architecture os) {
        return token switch {
            X64 => os is Architecture.X64 or Architecture.Arm64,
            Arm64 => os is Architecture.Arm64,
            _ => false
        };
    }
}
