using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.IO {
    internal abstract class StreamTextSource : IStreamText {
        public abstract long StreamLength { get; }

        public virtual bool StreamValid =>
            true;

        public virtual Task<IDisposable> StreamReady(CancellationToken cancellationToken) {
            return Task.FromResult(default(IDisposable));
        }

        public abstract Stream StreamText();
    }
}
