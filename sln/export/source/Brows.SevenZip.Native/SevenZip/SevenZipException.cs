using System;

namespace Brows.SevenZip;

internal sealed class SevenZipException : Exception {
    public int Error { get; }

    public SevenZipException(int error, string message) : base(
        string.IsNullOrWhiteSpace(message)
            ? $"The 7-Zip operation failed with error {error}."
            : $"The 7-Zip operation failed with error {error}: {message}") {
        Error = error;
    }
}
