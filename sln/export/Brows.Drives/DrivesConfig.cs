using System.Collections.Generic;

namespace Brows {
    internal sealed class DrivesConfig : EntryConfig {
        protected override IEnumerable<string> DefaultKeyInit() {
            return new[] {
                nameof(DriveInfoData.Name),
                nameof(DriveInfoData.VolumeLabel),
                nameof(DriveInfoData.TotalSize),
                nameof(DriveInfoData.TotalFreeSpace),
                nameof(DriveInfoData.AvailableFreeSpace)
            };
        }

        protected override IEnumerable<KeyValuePair<string, EntrySortDirection>> DefaultSortInit() {
            return new[] {
                KeyValuePair.Create(nameof(DriveInfoData.Name), EntrySortDirection.Ascending)
            };
        }
    }
}
