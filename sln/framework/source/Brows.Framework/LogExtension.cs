using Domore.Logs;

namespace Brows;

/// <summary>
/// Provides helpers for composing log messages.
/// </summary>
public static class LogExtension {
    /// <summary>
    /// Joins values into a log message separated by <c> &gt; </c>.
    /// </summary>
    /// <param name="log">
    /// The log for which the message is composed.
    /// </param>
    /// <param name="args">
    /// The values to include, in order.
    /// </param>
    /// <returns>
    /// The joined message.
    /// </returns>
    public static string Join(this ILog log, params object[] args) {
        return string.Join(" > ", args);
    }
}
