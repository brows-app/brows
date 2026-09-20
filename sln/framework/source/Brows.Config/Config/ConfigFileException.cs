using System;

namespace Brows.Config;

internal sealed class ConfigFileException : Exception {
    public ConfigFileException(string message) : base(message) {
    }

    public ConfigFileException(string message, Exception innerException) : base(message, innerException) {
    }
}
