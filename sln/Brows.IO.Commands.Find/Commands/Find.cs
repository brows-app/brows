using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Threading.Tasks;

    internal class Find : Command<Find.Parameter>, ICommandExport {
        private FindCommand FindCommand =>
            _FindCommand ?? (
            _FindCommand = new FindCommand());
        private FindCommand _FindCommand;

        private async Task<DirectoryInfo> Root(Context context, CancellationToken cancellationToken) {
            if (context == null) return null;
            if (context.HasPanel(out var panel)) {
                var path = panel.ID?.Value;
                if (path == null) {
                    return null;
                }
                var directory = await Async.With(cancellationToken).Run(() => {
                    var dir = default(DirectoryInfo);
                    try {
                        dir = new DirectoryInfo(path);
                    }
                    catch {
                        return null;
                    }
                    return dir.Exists ? dir : null;
                });
                if (directory != null) {
                    return directory;
                }
            }
            return null;
        }

        private async Task<bool> Work(Context context, PressGesture _, CancellationToken cancellationToken) {
            var root = await Root(context, cancellationToken);
            if (root == null) {
                return false;
            }
            if (context.HasCommander(out var commander)) {
                await commander.ShowPalette("find ", cancellationToken);
                return true;
            }
            return false;
        }

        private async Task<bool> Work(Context context, Parameter parameter, CancellationToken cancellationToken) {
            if (parameter == null) return false;

            var pattern = parameter.Pattern?.Trim() ?? "";
            if (pattern == "") return false;

            var root = await Root(context, cancellationToken);
            if (root == null) return false;

            if (context.HasInput(out var input)) {
                var find = new FindResult(input, root.FullName);
                var list = FindCommand.List;
                var data = new FindData(this, context, 0, list);
                var work = new FindWork(root, find, parameter);
                list.Insert(0, find);
                context.SetData(data);
                context.SetHint(data);
                context.SetFlag(new CommandContextFlag {
                    PersistInput = true,
                    RefreshInput = true
                });
                work.Work();
                return true;
            }
            return false;
        }

        protected override IAsyncEnumerable<ICommandSuggestion> Suggest(Context context, CancellationToken cancellationToken) {
            if (context != null) {
                if (context.DidTrigger(this)) {
                    if (context.HasData(out _) == false) {
                        var dataList = FindCommand.List;
                        if (dataList.Count > 0) {
                            var data = new FindData(this, context, 0, dataList);
                            context.SetData(data);
                            context.SetHint(data);
                        }
                    }
                }
            }
            return base.Suggest(context, cancellationToken);
        }

        protected override async Task<bool> Work(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasKey(out var key)) {
                return await Work(context, key, cancellationToken);
            }
            if (context.HasParameter(out var parameter)) {
                return await Work(context, parameter, cancellationToken);
            }
            return false;
        }

        public class Parameter {
            [Argument(Name = "pattern", Order = 0, Required = true)]
            public string Pattern { get; set; }

            [Switch(Name = "case-sensitive")]
            public bool CaseSensitive { get; set; }

            [Switch(Name = "in")]
            public FindIn In { get; set; } = FindIn.DirectoryName | FindIn.FileName;
        }
    }
}
