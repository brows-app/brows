using Brows.Exports;
using Domore.Conf;
using Domore.Conf.Cli;
using Domore.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class Delete : FileSystemCommand<Delete.Parameter> {
        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(FileSystemEntry),
            typeof(FileSystemTreeNode)
        };

        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == context.GetParameter(out var parameter)) return false;
            if (false == context.HasSourceFileSystemInfo(out _, out var list)) return false;
            return context.Operate(async (progress, token) => {
                switch (parameter.Mode) {
                    case null:
                    case Mode.Native:
                    default:
                        var service = NativeService;
                        if (service == null) {
                            return false;
                        }
                        var groups = list.GroupBy(info => Path.GetDirectoryName(info.FullName));
                        foreach (var group in groups) {
                            var names = group.Select(info => info.Name).ToList();
                            await service
                                .Work(names, group.Key, parameter, progress, token)
                                .ConfigureAwait(false);
                        }
                        return true;
                    case Mode.Managed:
                        var prog = new FileSystemOperationProgress(progress);
                        var tasks = list
                            .Select(info => FileSystemTask.Delete(info, prog, token))
                            .ToList();
                        await Task
                            .WhenAll(tasks)
                            .ConfigureAwait(false);
                        return tasks.Count > 0;
                }
            });
        }

        public IDeleteFilesInDirectory NativeService { get; set; }

        public enum Mode {
            [Conf("n", "native")]
            [CliDisplayOverride("(n)ative")]
            Native,

            [Conf("m", "managed")]
            [CliDisplayOverride("(m)anaged")]
            Managed
        }

        public sealed class Parameter : IDeleteFilesInDirectoryOptions {
            [Conf("u")]
            public bool? Unrecoverable { get; set; }
            public Mode? Mode { get; set; }
        }
    }
}
