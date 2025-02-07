using System;

namespace Brows.Url.Extensions {
    internal sealed class ClientForUrlException : Exception {
        public ClientForUrlException(string message, Exception innerException) : base(message, innerException) {
        }
    }
}
