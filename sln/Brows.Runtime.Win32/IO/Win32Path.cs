using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WIN32PATH = Domore.Runtime.Win32Path;

namespace Brows.IO {
    using Threading.Tasks;

    internal static class Win32Path {
        public static async Task<bool> AreSame(string path1, string path2, CancellationToken cancellationToken) {
            var uri1 = new Uri(path1);
            var uri2 = new Uri(path2);
            if (uri2.Equals(uri1)) {
                return true;
            }
            return await Async.With(cancellationToken).Run(() => {
                var path1Exists = File.Exists(path1);
                if (path1Exists == false) {
                    return false;
                }
                var path2Exists = File.Exists(path2);
                if (path2Exists == false) {
                    return false;
                }
                return WIN32PATH.AreSame(path1, path2);
            });
        }

        public static async Task<bool> IsCaseSensitive(string path, CancellationToken cancellationToken) {
            return await Async.With(cancellationToken).Run(() => {
                return WIN32PATH.IsCaseSensitive(path);
            });
        }
    }
}
