using Brows.SSH.Native;
using Domore.IO;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.SSH {
    internal abstract class SSHExecText : IStreamText {
        private SSHExecText() {
        }

        private sealed class StdOutText : SSHExecText {
            protected sealed override Stream StreamText() {
                return Exec.StdOut;
            }

            public Exec Exec { get; }

            public StdOutText(Exec exec) {
                Exec = exec ?? throw new ArgumentNullException(nameof(exec));
            }
        }

        private sealed class StdErrText : SSHExecText {
            protected sealed override Stream StreamText() {
                return Exec.StdErr;
            }

            public Exec Exec { get; }

            public StdErrText(Exec exec) {
                Exec = exec ?? throw new ArgumentNullException(nameof(exec));
            }
        }

        protected abstract Stream StreamText();

        public static SSHExecText StdOut(Exec exec) {
            return new StdOutText(exec);
        }

        public static SSHExecText StdErr(Exec exec) {
            return new StdErrText(exec);
        }

        long IStreamText.StreamLength =>
            throw new NotSupportedException();

        Task<IDisposable> IStreamText.StreamReady(CancellationToken token) {
            return Task.FromResult(default(IDisposable));
        }

        Stream IStreamText.StreamText() {
            return StreamText();
        }
    }
}
