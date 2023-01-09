using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    internal static class ConfigPath {
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

        public static async Task<string> Ready(CancellationToken cancellationToken) {
            return await Task.Run(cancellationToken: cancellationToken, function: async () => {
                return await CreateRoot(cancellationToken);
            });
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
