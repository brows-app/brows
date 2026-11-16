using Domore.Conf.Cli;
using System;

namespace Brows.Commands;

internal sealed class Ftp : Command<Ftp.Parameter> {
    private static bool Provide(Context context, Uri uri) {
        return context.Operate((progress, token) =>
            context.Provide(uri.ToString(), CommandContextProvide.ActivePanel, token));
    }

    internal static Uri ExplicitUri(Uri uri) {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.Scheme.Equals("ftp", StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("Explicit FTP TLS requires an ftp:// URI.", nameof(uri));
        }
        return new UriBuilder(uri) {
            Scheme = "ftpes",
            Query = "brows-tls=explicit",
            Fragment = ""
        }.Uri;
    }

    protected sealed override bool Work(Context context) {
        if (context is null || !context.GetParameter(out var parameter) || !Uri.TryCreate(parameter.Url, UriKind.Absolute, out var uri)) {
            return false;
        }
        if (parameter.Explicit) {
            try {
                uri = ExplicitUri(uri);
            }
            catch (ArgumentException) {
                return false;
            }
        }
        else {
            var schemeIsFtp = uri.Scheme.Equals("ftp", StringComparison.OrdinalIgnoreCase);
            var schemeIsFtps = uri.Scheme.Equals("ftps", StringComparison.OrdinalIgnoreCase);
            if (!schemeIsFtp && !schemeIsFtps) {
                return false;
            }
        }
        return Provide(context, uri);
    }

    public sealed class Parameter {
        [CliArgument]
        [CliRequired]
        public string Url { get; set; }
        public bool Explicit { get; set; }
    }
}
