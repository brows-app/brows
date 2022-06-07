using System.Collections.Generic;
using System.IO;

namespace Brows {
    internal class DriveEntryData {
        public static IReadOnlyDictionary<string, IEntryColumn> Columns =>
            DriveInfoWrapper.Columns;

        public static IReadOnlySet<string> Options =>
            DriveInfoWrapper.Keys;

        public static IReadOnlySet<string> Defaults { get; } = new HashSet<string> {
            nameof(DriveInfo.Name),
            nameof(DriveInfo.VolumeLabel),
            nameof(DriveInfo.TotalSize),
            nameof(DriveInfo.TotalFreeSpace),
            nameof(DriveInfo.AvailableFreeSpace)
        };

        public static IComponentResourceKey Resolver { get; } = new DriveComponentKey();
    }
}
