using Brows.DirectoryListing;
using System;

namespace Brows;

internal static class FtpListingItemExtension {
    public static FileProtocolEntryKind EntryKind(this DirectoryListItem item) {
        ArgumentNullException.ThrowIfNull(item);
        var isDirectory = item.Flags.HasFlag(DirectoryListItemFlags.Directory);
        var isLink = item.Flags.HasFlag(DirectoryListItemFlags.Link);
        if (isDirectory) {
            return isLink ? FileProtocolEntryKind.DirectoryLink : FileProtocolEntryKind.Directory;
        }
        return isLink ? FileProtocolEntryKind.FileLink : FileProtocolEntryKind.File;
    }
}
