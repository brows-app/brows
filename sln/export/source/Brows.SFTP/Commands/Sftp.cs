using Domore.Conf.Cli;
using System;

namespace Brows.Commands;

internal sealed class Sftp : Command<Sftp.Parameter> {
    protected sealed override bool Work(Context context) {
        if (context is null || !context.HasParameter(out var parameter)) {
            return false;
        }
        var path = parameter.Path ?? "/";
        if (!path.StartsWith("/", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(parameter.Host)) {
            return false;
        }
        var builder = new UriBuilder("sftp", parameter.Host) {
            Port = parameter.Port ?? 22,
            UserName = parameter.User,
            Path = path
        };
        var uri = builder.Uri;
        return context.Operate(async (_, token) => {
            return await context.Provide(uri.ToString(), CommandContextProvide.ActivePanel, token);
        });
    }

    public sealed class Parameter {
        [CliArgument]
        [CliRequired]
        public string Host { get; set; }

        [CliArgument]
        public string Path { get; set; }

        public string User { get; set; }
        public int? Port { get; set; }
    }
}
