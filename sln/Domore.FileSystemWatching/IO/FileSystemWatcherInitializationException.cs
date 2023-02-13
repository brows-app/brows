using System;

namespace Domore.IO {
    internal class FileSystemWatcherInitializationException : Exception {
        public FileSystemWatcherInitializationException(string message, Exception innerException) : base(message, innerException) {
        }
    }
}
