using System;
using System.Collections.Generic;

namespace Brows;

internal sealed class FileSystemFindData : CommandContextData<FileSystemFindResult>, ICommandContextHint {
    private bool LoadInput { get; }
    private bool LoadConf { get; }
    private FileSystemFindResult SelectedResult { get; }

    private FileSystemFindResult CurrentResult => SelectedResult;

    private FileSystemFindData(
        ICommand command,
        ICommandContext context,
        int index,
        bool loadInput,
        bool loadConf,
        IList<FileSystemFindResult> list,
        FileSystemFindResult selectedResult)
    : base(command, index, list) {
        LoadInput = loadInput;
        LoadConf = loadConf;
        SelectedResult = selectedResult;
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    private static FileSystemFindResult GetItem(int index, IList<FileSystemFindResult> list) {
        ArgumentNullException.ThrowIfNull(list);
        var validIndex = index >= 0 && list.Count > index;
        return validIndex ? list[index] : null;
    }

    private FileSystemFindData CreateData(int index, IList<FileSystemFindResult> list) {
        return new FileSystemFindData(
            Command,
            Context,
            index,
            loadInput: true,
            loadConf: true,
            list: list,
            selectedResult: GetItem(index, list));
    }

    protected sealed override CommandContextData<FileSystemFindResult> Create(
        int index,
        IList<FileSystemFindResult> list) {
        return CreateData(index, list);
    }

    public sealed override object Current => CurrentResult;

    public sealed override string Input => LoadInput ? CurrentResult?.Input : null;
    public sealed override string Conf => LoadConf ? CurrentResult?.Conf : null;

    public ICommandContext Context { get; }

    public FileSystemFindData(
        ICommand command,
        ICommandContext context,
        int index,
        IList<FileSystemFindResult> list)
    : this(command, context, index, false, false, list, GetItem(index, list)) {
    }

    public sealed override ICommandContextData Remove() {
        var current = CurrentResult;
        if (current is null) {
            return null;
        }

        var index = List.IndexOf(current);
        if (index < 0) {
            if (List.Count == 0) {
                return null;
            }
            return CreateData(Math.Clamp(Index, 0, List.Count - 1), List);
        }
        List.RemoveAt(index);
        Removed(current);
        if (List.Count == 0) {
            return null;
        }
        return CreateData(Math.Min(index, List.Count - 1), List);
    }

    public sealed override ICommandContextData Next() {
        if (List.Count == 0) {
            return null;
        }
        var currentIndex = List.IndexOf(CurrentResult);
        if (currentIndex < 0) {
            return CreateData(Index >= List.Count ? 0 : Math.Max(0, Index), List);
        }
        var nextIndex = currentIndex == List.Count - 1
            ? 0
            : currentIndex + 1;
        return CreateData(nextIndex, List);
    }

    public sealed override ICommandContextData Previous() {
        if (List.Count == 0) {
            return null;
        }
        var currentIndex = List.IndexOf(CurrentResult);
        if (currentIndex < 0) {
            return CreateData(Index <= 0 ? List.Count - 1 : Math.Min(Index - 1, List.Count - 1), List);
        }
        var previousIndex = currentIndex <= 0
            ? List.Count - 1
            : currentIndex - 1;
        return CreateData(previousIndex, List);
    }

    public sealed override ICommandContextData Clear() {
        var current = CurrentResult;
        var currentIndex = List.IndexOf(current);
        if (currentIndex < 0) {
            currentIndex = Index;
        }
        for (var i = List.Count - 1; i >= 0; i--) {
            if (List[i].Complete) {
                List.RemoveAt(i);
            }
        }

        if (List.Count == 0) {
            return null;
        }

        if (current is not null) {
            currentIndex = List.IndexOf(current);
        }
        if (currentIndex < 0) {
            currentIndex = Math.Clamp(Index, 0, List.Count - 1);
        }
        return CreateData(currentIndex, List);
    }

    public sealed override ICommandContextData Enter() {
        var findItem = CurrentResult?.CurrentItem;
        if (findItem is not null) {
            var target = findItem.TargetDirectory;
            Context.Operate(async (progress, token) => {
                return await Context.Provide(target, CommandContextProvide.ActivePanel, token);
            });
        }
        Flag = new CommandContextFlag { PersistInput = false };
        return this;
    }
}
