using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Data {
    internal static class FileSystemEntryData {
        public sealed class Extension : Definition<string> {
            public Extension() : base(e => e.Extension) {
                Width = 50;
            }
        }

        public sealed class Name : Definition<string> {
            public Name() : base(e => e.Name) {
                Width = 250;
            }
        }

        public sealed class Kind : Definition<FileSystemEntryKind> {
            public Kind() : base(e => e.Kind) {
                Width = 75;
            }
        }

        public sealed class Path : Definition<string> {
            public Path() : base(e => e.ID) {
                Width = 250;
            }

            public sealed override bool SuggestKey(ICommandContext context) {
                return false;
            }
        }

        public abstract class Definition<T> : FileSystemEntryData<T> {
            protected Func<FileSystemEntry, T> Func { get; }

            protected Definition(Func<FileSystemEntry, T> func) {
                Func = func ?? throw new ArgumentNullException(nameof(func));
            }

            protected sealed override Task<T> GetValue(FileSystemEntry entry, Action<T> progress, CancellationToken cancellationToken) {
                if (null == entry) throw new ArgumentNullException(nameof(entry));
                return Task.FromResult(Func(entry));
            }
        }
    }

    internal abstract class FileSystemEntryData<T> : EntryDataDefinition<FileSystemEntry, T> {
        public sealed override string Group =>
            nameof(FileSystemEntryData);
    }
}
