using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Brows {
    public static class DrivePanel {
        public static bool HasDriveSelection(this IPanel panel, out IReadOnlyList<DriveInfo> info) {
            if (panel is null) throw new ArgumentNullException(nameof(panel));
            if (panel.HasProvider(out DriveProvider _) == false) {
                info = null;
                return false;
            }
            if (panel.HasSelection(out var entries) == false) {
                info = null;
                return false;
            }
            info = entries.OfType<DriveEntry>().Select(entry => entry.Info).ToList();
            return info.Count > 0;
        }
    }
}
