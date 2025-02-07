using Microsoft.Win32;
using System;

namespace Brows.Security {
    internal static class RegistryPath {
        public static void Open(Action<RegistryKey> action) {
            ArgumentNullException.ThrowIfNull(action);
            using
            var hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            if (hive == null) {
                throw new InvalidOperationException("Cannot open base key");
            }
            using
            var brows = hive.CreateSubKey("Brows", writable: true);
            if (brows == null) {
                throw new InvalidOperationException("Cannot open sub key");
            }
            using
            var security = brows.CreateSubKey("Security", writable: true);
            if (security == null) {
                throw new InvalidOperationException("Cannot open security key");
            }
            action(security);
        }
    }
}
