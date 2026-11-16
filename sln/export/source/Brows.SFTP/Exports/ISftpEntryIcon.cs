using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports;

internal interface ISftpEntryIcon : IExport {
    Task<bool> Work(FileProtocolEntryKind kind, string extension, Action<object> set, CancellationToken token);
}
