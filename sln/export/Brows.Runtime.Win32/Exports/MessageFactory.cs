using Brows.Windows;
using Domore.Logs;
using Domore.Runtime.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace Brows.Exports {
    internal sealed class MessageFactory : IMessageFactory {
        private static readonly ILog Log = Logging.For(typeof(MessageFactory));

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
            private nint Alloc { get; }
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
                try {
                    Source.RemoveHook(Hook);
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                }
                try {
                    var unregistered = user32.UnregisterDeviceNotification(Hwnd);
                    if (unregistered == 0) {
                        try {
                            throw new Win32Exception();
                        }
                        catch (Exception ex) {
                            if (Log.Error()) {
                                Log.Error(ex);
                            }
                        }
                    }
                    Marshal.FreeHGlobal(Alloc);
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                }
            }

            public event MessageEventHandler Message;

            public nint Hwnd { get; }

            public Messenger(nint hwnd) {
                Hwnd = hwnd;
                Source = HwndSource.FromHwnd(Hwnd);
                Source.AddHook(Hook);
                //var s = new DEV_BROADCAST_DEVICEINTERFACE_W {
                //    dbcc_classguid = GUID_DEVINTERFACE.USB_DEVICE,
                //    dbcc_devicetype = (uint)DBT_DEVTYP.DEVICEINTERFACE,
                //    dbcc_name = 0,
                //    dbcc_reserved = 0,
                //    dbcc_size = (uint)Marshal.SizeOf<DEV_BROADCAST_DEVICEINTERFACE_W>()
                //};
                //Alloc =
                //Marshal.AllocHGlobal((int)s.dbcc_size);
                //Marshal.StructureToPtr(s, Alloc, true);
                //user32.RegisterDeviceNotificationW(Hwnd, Alloc, DEVICE_NOTIFY.WINDOW_HANDLE);
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
