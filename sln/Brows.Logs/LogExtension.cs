using Domore.Logs;

namespace Brows {
    public static class LogExtension {
        public static string Join(this ILog log, params object[] args) {
            return string.Join(" > ", args);
        }
    }
}
