﻿namespace Brows {
    internal class DriveComponentKey : IComponentResourceKey {
        public object For(string key) {
            return nameof(DriveEntryData) + "_" + key;
        }
    }
}
