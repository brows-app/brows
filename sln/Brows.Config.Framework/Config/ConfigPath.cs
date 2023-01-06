using Domore.Converting;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    internal class ConfigPath {
        private static string Encode(string s) {
            var utf8bytes = Encoding.UTF8.GetBytes(s);
            var base32hex = Base32HexString.From(utf8bytes);
            var sanitized = base32hex.Replace('=', '_');
            return sanitized;
        }

        private static string LocalApplicationData() {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);
        }

        private static async Task<string> CreateRoot(CancellationToken cancellationToken) {
            var root = Root;
            if (Directory.Exists(root) == false) {
                Directory.CreateDirectory(root);
                for (; ; ) {
                    if (Directory.Exists(root)) {
                        break;
                    }
                    await Task.Delay(10, cancellationToken);
                }
            }
            return root;
        }

        public async Task<string> Ready(string type, string id, string extension, CancellationToken cancellationToken) {
            var root = await Task.Run(() => CreateRoot(cancellationToken), cancellationToken);
            var file = Encode("T=" + type + ";ID=" + id);
            var path = Path.Combine(root, Path.ChangeExtension(file, extension));
            return path;
        }

        public static string Root =>
            _Root ?? (
            _Root = Path.Combine(
                LocalApplicationData(),
                "Brows",
                "data"));
        private static string _Root;
    }
}
