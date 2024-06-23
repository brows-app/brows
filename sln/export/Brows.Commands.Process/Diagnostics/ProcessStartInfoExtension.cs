using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Brows.Diagnostics {
    internal static class ProcessStartInfoExtension {
        public static void Environment(this ProcessStartInfo processStartInfo, IReadOnlyDictionary<string, string> append) {
            ArgumentNullException.ThrowIfNull(processStartInfo);
            var environment = processStartInfo.Environment;
            if (environment != null) {
                if (append != null) {
                    foreach (var item in append) {
                        environment[item.Key] = item.Value;
                    }
                }
            }
        }
    }
}
