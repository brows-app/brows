using Domore.Logs;
using System;
using System.Text.RegularExpressions;

namespace Brows.Updates;

internal static class UpdateRepository {
    private const string Default = "brows-app/brows";
    private static readonly ILog Log = Logging.For(typeof(UpdateRepository));

    internal static string Current => field ??= From(Environment.GetEnvironmentVariable("BROWS_UPDATE_REPOSITORY"));

    internal static string From(string value) {
        if (value is null) {
            return Default;
        }
        if (Regex.IsMatch(value, @"\A[A-Za-z0-9-]+/(?!\.{1,2}\z)[A-Za-z0-9._-]+\z")) {
            return value;
        }
        if (Log.Warn()) {
            Log.Warn("Ignoring an invalid BROWS_UPDATE_REPOSITORY value.");
        }
        return Default;
    }
}
