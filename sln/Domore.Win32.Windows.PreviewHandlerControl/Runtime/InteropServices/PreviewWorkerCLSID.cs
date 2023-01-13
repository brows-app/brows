using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.Runtime.InteropServices {
    using ComTypes;
    using Threading;
    using Win32;

    internal class PreviewWorkerCLSID {
        private static readonly ConcurrentDictionary<string, Guid> Cache = new ConcurrentDictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        private async Task<Guid> Get(string extension, CancellationToken cancellationToken) {
            return await ThreadPool.Work(nameof(PreviewWorkerCLSID), cancellationToken: cancellationToken, work: () => {
                var sb = new StringBuilder(256);
                var cap = (uint)sb.Capacity;
                var hr = shlwapi.AssocQueryStringW(
                    flags: ASSOCF.INIT_DEFAULTTOSTAR | ASSOCF.NOTRUNCATE,
                    str: ASSOCSTR.SHELLEXTENSION,
                    pszAssoc: extension,
                    pszExtra: $"{{{IID.IPreviewHandler}}}",
                    pszOut: sb,
                    pcchOut: ref cap);
                switch (hr) {
                    case (HRESULT)0x80070483:
                        return Guid.Empty;
                    default:
                        hr.ThrowOnError();
                        break;
                }
                var guid = sb.ToString();
                return new Guid(guid);
            });
        }

        public STAThreadPool ThreadPool { get; }

        public PreviewWorkerCLSID(STAThreadPool threadPool) {
            ThreadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        }

        public async Task<Guid> For(string extension, CancellationToken cancellationToken) {
            if (string.IsNullOrWhiteSpace(extension)) {
                return Guid.Empty;
            }
            if (Cache.TryGetValue(extension, out var value) == false) {
                Cache[extension] = value = await Get(extension, cancellationToken);
            }
            return value;
        }
    }
}
