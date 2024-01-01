using System;

namespace Brows.Native {
    public sealed class NativeErrorException : Exception {
        public int Error { get; }

        public NativeErrorException(int error) {
            Error = error;
        }
    }
}
