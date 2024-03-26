using Domore.Logs;
using Domore.Runtime.Win32;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Brows.Windows {
    internal static class Win32Message {
        private static readonly ILog Log = Logging.For(typeof(Win32Message));
        private static readonly IReadOnlyDictionary<uint, char> DriveMap = new Dictionary<uint, char> {
            { 0b0000000000000000000000000001, 'A' },
            { 0b0000000000000000000000000010, 'B' },
            { 0b0000000000000000000000000100, 'C' },
            { 0b0000000000000000000000001000, 'D' },
            { 0b0000000000000000000000010000, 'E' },
            { 0b0000000000000000000000100000, 'F' },
            { 0b0000000000000000000001000000, 'G' },
            { 0b0000000000000000000010000000, 'H' },
            { 0b0000000000000000000100000000, 'I' },
            { 0b0000000000000000001000000000, 'J' },
            { 0b0000000000000000010000000000, 'K' },
            { 0b0000000000000000100000000000, 'L' },
            { 0b0000000000000001000000000000, 'M' },
            { 0b0000000000000010000000000000, 'N' },
            { 0b0000000000000100000000000000, 'O' },
            { 0b0000000000001000000000000000, 'P' },
            { 0b0000000000010000000000000000, 'Q' },
            { 0b0000000000100000000000000000, 'R' },
            { 0b0000000001000000000000000000, 'S' },
            { 0b0000000010000000000000000000, 'T' },
            { 0b0000000100000000000000000000, 'U' },
            { 0b0000001000000000000000000000, 'V' },
            { 0b0000010000000000000000000000, 'W' },
            { 0b0000100000000000000000000000, 'X' },
            { 0b0001000000000000000000000000, 'Y' },
            { 0b0010000000000000000000000000, 'Z' }
        };

        private static DeviceChangeInfo DeviceChangeInfo(nint lParam) {
            var s = Marshal.PtrToStructure<DEV_BROADCAST_HDR>(lParam);
            switch (s.dbch_devicetype) {
                case DBT_DEVTYP.VOLUME:
                    var t = Marshal.PtrToStructure<DEV_BROADCAST_VOLUME>(lParam);
                    var f = t.dbcv_flags;
                    var m = t.dbcv_unitmask;
                    return new DeviceChangeVolume {
                        Drive = DriveMap
                            .Where(pair => (pair.Key & m) == pair.Key)
                            .Select(pair => pair.Value)
                            .ToHashSet(),
                        Flag =
                            f == DBTF.MEDIA ? DeviceChangeVolumeFlag.Media :
                            f == DBTF.NET ? DeviceChangeVolumeFlag.Network :
                            DeviceChangeVolumeFlag.None,
                        Type = DeviceChangeType.Volume
                    };
            }
            return null;
        }

        private static DeviceChange DeviceChange(DeviceChangeKind kind, nint lParam) {
            var info = DeviceChangeInfo(lParam);
            if (info != null) {
                return new DeviceChange { Info = info, Kind = kind };
            }
            return null;
        }

        private static IMessage Factory(int msg, nint wParam, nint lParam) {
#if DEBUG
            if (Log.Debug()) {
                Log.Debug($"{nameof(Factory)}({nameof(msg)}: {msg}, {nameof(wParam)}: {wParam}, {nameof(lParam)}: {lParam})");
            }
#endif
            var wmMsg = (WM)msg;
            switch (wmMsg) {
                case WM.DEVICECHANGE:
                    var dbtParam = (DBT)wParam;
                    switch (dbtParam) {
                        case DBT.DEVICEARRIVAL:
                            return DeviceChange(DeviceChangeKind.Arrival, lParam);
                        case DBT.DEVICEREMOVECOMPLETE:
                            return DeviceChange(DeviceChangeKind.RemovalComplete, lParam);
                        case DBT.DEVICEREMOVEPENDING:
                            return DeviceChange(DeviceChangeKind.RemovalPending, lParam);
                        case DBT.DEVICEQUERYREMOVE:
                            return DeviceChange(DeviceChangeKind.RemovalRequest, lParam);
                        case DBT.DEVICEQUERYREMOVEFAILED:
                            return DeviceChange(DeviceChangeKind.RemovalCanceled, lParam);
                    }
                    break;
            }
            return null;
        }

        public static IMessage Interpret(int msg, nint wParam, nint lParam) {
            var message = Factory(msg, wParam, lParam);
            if (message != null) {
                if (Log.Info()) {
                    Log.Info(Log.Join(nameof(Interpret), message));
                }
                return message;
            }
            else {
#if DEBUG
                if (Log.Debug()) {
                    Log.Debug(Log.Join(nameof(Interpret), "N/A"));
                }
#endif
                return null;
            }
        }
    }
}
