using Domore.Runtime.Win32;
using System;

namespace Brows {
    internal sealed class ProgramConsole : IProgramConsole, IDisposable {
        private readonly object Locker = new();
        private bool Alloc;
        private bool Shown;

        public bool Show() {
            if (Shown == false) {
                lock (Locker) {
                    if (Shown == false) {
                        Shown = true;
                        Alloc = kernel32.AttachConsole(-1)
                            ? false
                            : kernel32.AllocConsole();
                        return true;
                    }
                }
            }
            return false;
        }

        public bool Hide() {
            if (Shown) {
                lock (Locker) {
                    if (Shown) {
                        Shown = false;
                        if (Alloc) {
                            Alloc = false;
                            try {
                                kernel32.FreeConsole();
                            }
                            catch {
                            }
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        void IDisposable.Dispose() {
            if (Alloc) {
                try {
                    kernel32.FreeConsole();
                }
                catch {
                }
            }
        }
    }
}
