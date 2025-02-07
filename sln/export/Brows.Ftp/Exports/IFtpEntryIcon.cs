using Brows.Url.Ftp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IFtpEntryIcon : IExport {
        Task<bool> Work(FtpListingInfo info, Action<object> set, CancellationToken token);
    }
}
