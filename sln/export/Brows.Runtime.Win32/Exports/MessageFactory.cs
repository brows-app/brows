using Brows.Windows;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace Brows.Exports {
    internal sealed class MessageFactory : IMessageFactory {
        async Task<bool> IMessageFactory.Work(object window, ICollection<IMessenger> set, CancellationToken token) {
            if (set == null) {
                return false;
            }
            var hwnd =
                window is nint n ? n :
                window is Window w ? new WindowInteropHelper(w).Handle :
                0;
            if (hwnd == 0) {
                return false;
            }
            var msg = new Messenger(hwnd);
            set.Add(msg);
            return await Task.FromResult(true);
        }

        private sealed class Messenger : IMessenger {
            private HwndSource Source { get; }

            private nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled) {
                if (Hwnd == hwnd) {
                    var message = Win32Message.Interpret(msg, wParam, lParam);
                    if (message != null) {
                        Message?.Invoke(this, new MessageEventArgs(message));
                    }
                }
                handled = false;
                return 0;
            }

            private void Dispose(bool disposing) {
                if (disposing) {
                    Message = null;
                }
                Source.RemoveHook(Hook);
            }

            public event MessageEventHandler Message;

            public nint Hwnd { get; }

            public Messenger(nint hwnd) {
                Hwnd = hwnd;
                Source = HwndSource.FromHwnd(Hwnd);
                Source.AddHook(Hook);
            }

            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            ~Messenger() {
                Dispose(false);
            }
        }
    }
}
