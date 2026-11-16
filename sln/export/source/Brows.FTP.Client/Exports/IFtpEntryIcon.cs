using Brows.DirectoryListing;
using System;
using System.Threading.Tasks;

namespace Brows.Exports;

public interface IFtpEntryIcon : IExport {
    Task<bool> Work(DirectoryListItem info, Action<object> set, CancellationToken token);
}
