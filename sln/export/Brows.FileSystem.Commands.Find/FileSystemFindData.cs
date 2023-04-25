using System;
using System.Collections.Generic;

namespace Brows {
    internal sealed class FileSystemFindData : CommandContextData<FileSystemFindResult>, ICommandContextHint {
        private bool LoadInput { get; }
        private bool LoadConf { get; }

        private FileSystemFindData(ICommand command, ICommandContext context, int index, bool loadInput, bool loadConf, IList<FileSystemFindResult> list) : base(command, index, list) {
            LoadInput = loadInput;
            LoadConf = loadConf;
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        protected sealed override CommandContextData<FileSystemFindResult> Create(int index, IList<FileSystemFindResult> list) {
            return new FileSystemFindData(Command, Context, index, true, true, list);
        }

        public sealed override string Input => LoadInput ? Item?.Input : null;
        public sealed override string Conf => LoadConf ? Item?.Conf : null;

        public ICommandContext Context { get; }

        public FileSystemFindData(ICommand command, ICommandContext context, int index, IList<FileSystemFindResult> list) : this(command, context, index, false, false, list) {
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
