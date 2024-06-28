using System;

namespace Domore.IO {
    internal sealed class FileSystemWatcherInitializationException : Exception {
        public FileSystemWatcherInitializationException(string message, Exception innerException) : base(message, innerException) {
        }
    }
}
