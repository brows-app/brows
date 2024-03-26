using Domore.Logs;
using Domore.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    public static class DirectoryX {
        private static readonly ILog Log = Logging.For(typeof(DirectoryX));

        private static readonly TaskCache<IReadOnlyDictionary<string, string>> EnvironmentVariablePathsTask = new(async token => {
            return await Task.Run(cancellationToken: token, function: () => {
                var dict = new Dictionary<string, string>();
                var vars = Environment.GetEnvironmentVariables();
                var keys = vars?.Keys;
                if (keys != null) {
                    foreach (var key in keys) {
                        if (token.IsCancellationRequested) {
                            token.ThrowIfCancellationRequested();
                        }
                        var value = vars[key];
                        var pathMaybe = $"{value}";
                        var pathExists = Path.IsPathFullyQualified(pathMaybe) && Directory.Exists(pathMaybe);
                        if (pathExists) {
                            if (Log.Info()) {
                                Log.Info($"Env var path > {key} > {pathMaybe}");
                            }
                            dict[$"{key}"] = pathMaybe;
                        }
                        else {
                            if (Log.Debug()) {
                                Log.Debug($"Env var not a path > {key} > {pathMaybe}");
                            }
                        }
                    }
                }
                return dict;
            });
        });

        private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();
        private static readonly char DirectorySeparatorChar = Path.DirectorySeparatorChar;
        private static readonly char AltDirectorySeparatorChar = Path.AltDirectorySeparatorChar;

        public static string UserProfilePath => _UserProfilePath ??=
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
        private static string _UserProfilePath;

        public static bool Invalid(string path) {
            if (path is null) return true;
            if (path == string.Empty) return true;
            var pLen = path.Length;
            var invalid = InvalidPathChars;
            var invalidLen = invalid.Length;
            for (var p = 0; p < pLen; p++) {
                for (var i = 0; i < invalidLen; i++) {
                    if (invalid[i] == path[p]) {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool Separated(string path) {
            if (path is null) return false;
            if (path == string.Empty) return false;
            var pLen = path.Length;
            for (var i = 0; i < pLen; i++) {
                var c = path[i];
                if (c == DirectorySeparatorChar) {
                    return true;
                }
                if (c == AltDirectorySeparatorChar) {
                    return true;
                }
            }
            return false;
        }

        public static async Task<IReadOnlyDictionary<string, string>> EnvironmentVariablePaths(CancellationToken token) {
            return await EnvironmentVariablePathsTask.Ready(token);
        }
    }
}
