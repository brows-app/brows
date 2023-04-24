using System;
using System.IO;

namespace Domore.IO {
    internal abstract class StreamTextSource : IStreamText {
        public abstract long StreamLength { get; }

        public virtual bool StreamValid =>
            true;

        public virtual IDisposable StreamReady() {
            return null;
        }

        public abstract Stream StreamText();
    }
}
