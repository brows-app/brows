using Domore.Conf.Cli;
using System;
using System.IO;

namespace Brows.Commands {
    internal sealed class SSH : Command<SSH.Parameter> {
        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasParameter(out var parameter)) return false;
            var
            ub = new UriBuilder();
            ub.Scheme = "ssh";
            ub.Host = parameter.Host;
            ub.Port = parameter.Port ?? 22;
            ub.UserName = parameter.User;
            var uri = ub.Uri?.ToString()?.TrimEnd('/');
            if (uri == null) {
                return false;
            }
            var path = parameter.Path ?? "/";
            if (Path.IsPathRooted(path) == false) {
                return false;
            }
            var id = $"{uri}>{path}";
            return context.Operate(async (progress, token) => {
                return await context.Provide(id, CommandContextProvide.ActivePanel, token);
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
}
