using Brows.Exports;
using Domore.Conf.Cli;
using System;
using System.IO;

namespace Brows.Commands;

internal sealed class Status : Command<Status.Parameter> {
    protected sealed override bool Work(Context context) {
        if (context is null) {
            return false;
        }
        if (!context.GetParameter(out var parameter)) {
            return false;
        }
        var ppath = parameter.Path;
        var where = parameter.Where;
        var directory = context.HasPanel(out var panel) && panel.HasProvider(out var provider)
            ? provider switch {
                GitStatusProvider git => git.RepositoryPath,
                IFileSystemProvider fileSystem => fileSystem.Path,
                _ => null
            }
            : null;
        if (string.IsNullOrWhiteSpace(ppath) && directory is null) {
            return false;
        }
        return context.Operate(async (progress, token) => {
            string path = string.IsNullOrWhiteSpace(ppath)
                ? directory
                : Path.GetFullPath(ppath, directory ?? Environment.CurrentDirectory);
            var status = await GitStatusReader.Read(path, token, refresh: true);
            if (status is null) {
                throw new DirectoryNotFoundException($"No Git repository was found at or above '{path}'.");
            }
            var root = status.IsBare ? status.GitDirectory : status.WorkDirectory;
            return await context.Provide(GitStatusProviderFactory.IDFor(root), where, token);
        });
    }

    public sealed class Parameter {
        [CliArgument]
        public string Path { get; set; }

        public CommandContextProvide Where { get; set; }
    }
}
