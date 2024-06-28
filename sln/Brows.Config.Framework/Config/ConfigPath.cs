using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    public static class ConfigPath {
        private static Task<string> Ready(string directory, CancellationToken cancellationToken) {
            return Task.Run(cancellationToken: cancellationToken, function: async () => {
                if (Directory.Exists(directory) == false) {
                    Directory.CreateDirectory(directory);
                    for (; ; ) {
                        if (Directory.Exists(directory)) {
                            break;
                        }
                        await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                    }
                }
                return directory;
            });
        }

        public static Task<string> DataReady(CancellationToken cancellationToken) {
            return Ready(DataRoot, cancellationToken);
        }

        public static Task<string> FileReady(CancellationToken cancellationToken) {
            return Ready(FileRoot, cancellationToken);
        }

        public static string DataRoot =>
            _DataRoot ?? (
            _DataRoot = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData,
                    Environment.SpecialFolderOption.DoNotVerify),
                "Brows",
                "data"));
        private static string _DataRoot;

        public static string FileRoot =>
            _FileRoot ?? (
            _FileRoot = Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath),
                "brows.config"));
        private static string _FileRoot;
    }
}
