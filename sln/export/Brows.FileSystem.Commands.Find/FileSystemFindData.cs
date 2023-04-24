using System;
using System.Collections.Generic;

namespace Brows {
    internal sealed class FileSystemFindData : CommandContextData<FileSystemFindResult>, ICommandContextHint {
        protected sealed override CommandContextData<FileSystemFindResult> Create(int index, IList<FileSystemFindResult> list) {
            return new FileSystemFindData(Command, Context, index, list);
        }

        public sealed override string Input => Item?.Input;
        public sealed override string Conf => Item?.Conf;

        public ICommandContext Context { get; }

        public FileSystemFindData(ICommand command, ICommandContext context, int index, IList<FileSystemFindResult> list) : base(command, index, list) {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed override ICommandContextData Enter() {
            var findItem = Item?.CurrentItem;
            if (findItem != null) {
                var target = findItem.TargetDirectory;
                Context.Operate(async (progress, token) => {
                    return await Context.Provide(target, CommandContextProvide.ActivePanel, token);
                });
            }
            Flag = new CommandContextFlag { PersistInput = false };
            return this;
        }
    }
}
