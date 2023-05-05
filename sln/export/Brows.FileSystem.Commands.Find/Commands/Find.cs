using System.Collections.Generic;
using System.Threading;

namespace Brows.Commands {
    internal sealed class Find : FileSystemCommand<FindParameter> {
        private FileSystemFindCommand FindCommand =>
            _FindCommand ?? (
            _FindCommand = new FileSystemFindCommand());
        private FileSystemFindCommand _FindCommand;

        protected sealed override IAsyncEnumerable<ICommandSuggestion> Suggest(Context context, CancellationToken token) {
            if (context != null) {
                if (context.DidTrigger(this)) {
                    if (context.HasData(out _) == false) {
                        var dataList = FindCommand.List;
                        if (dataList.Count > 0) {
                            var data = new FileSystemFindData(this, context, 0, dataList);
                            context.SetData(data);
                            context.SetHint(data);
                        }
                    }
                }
            }
            return base.Suggest(context, token);
        }

        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasGesture(out _)) {
                if (context.HasCommander(out var commander)) {
                    return context.Operate(async (progress, token) => {
                        return await commander.ShowPalette($"{InputTrigger} ", token);
                    });
                }
            }
            if (context.HasInput(out var input) == false) return false;
            if (context.HasParameter(out var parameter) == false) return false;
            if (context.HasPanel(out var active) == false) {
                return false;
            }
            if (active.HasFileSystemDirectory(out var directory) == false) {
                return false;
            }
            if (string.IsNullOrWhiteSpace(parameter.Pattern)) {
                return false;
            }
            var conf = context.HasConf(out var c) ? c.Text : null;
            var work = new FindWork(new FileSystemFindResult(directory, parameter, input, conf));
            var list = FindCommand.List;
            var data = new FileSystemFindData(this, context, 0, list);
            list.Insert(0, work.Result);
            context.SetData(data);
            context.SetHint(data);
            context.SetFlag(new CommandContextFlag {
                PersistInput = true,
                RefreshInput = false
            });
            return context.Operate(async (progress, token) => {
                await work.Done(progress, token);
                return true;
            });
        }
    }
}
