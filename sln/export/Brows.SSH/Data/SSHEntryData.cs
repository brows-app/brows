using Brows.Exports;
using Brows.SSH;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Data {
    internal static class SSHEntryData {
        public sealed class Length : Definition<long?> {
            public Length() : base(i => i.Kind == SSHEntryKind.File ? i.Length : null) {
                Width = 100;
                Converter = EntryDataConverter.FileSystemSize;
                Alignment = EntryDataAlignment.Right;
            }
        }

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

        public sealed class Kind : Definition<SSHEntryKind> {
            public Kind() : base(e => e.Kind) {
                Width = 75;
            }
        }

        public sealed class Path : Definition<string> {
            public Path() : base(e => e.Path) {
                Width = 250;
            }

            public sealed override Task<bool> SuggestKey(ICommandContext context, CancellationToken token) {
                return Task.FromResult(false);
            }
        }

        public sealed class Permissions : Definition<string> {
            public Permissions() : base(i => i.Permissions) {
                Width = 175;
            }
        }

        public sealed class LinkTarget : Definition<string> {
            public LinkTarget() : base(i => i.LinkTarget) {
                Width = 100;
            }
        }

        public sealed class OwnedByUser : Definition<string> {
            public OwnedByUser() : base(i => i.OwnedByUser) {
                Width = 175;
            }
        }

        public sealed class OwnedByGroup : Definition<string> {
            public OwnedByGroup() : base(i => i.OwnedByGroup) {
                Width = 175;
            }
        }

        public sealed class CreationTime : Definition<DateTime?> {
            public CreationTime() : base(i => i.CreationTime) {
                Width = 175;
                Converter = EntryDataConverter.DateTime;
            }
        }

        public sealed class CreationTimeUtc : Definition<DateTime?> {
            public CreationTimeUtc() : base(i => i.CreationTimeUtc) {
                Width = 175;
                Converter = EntryDataConverter.DateTime;
            }
        }

        public sealed class LastAccessTime : Definition<DateTime?> {
            public LastAccessTime() : base(i => i.LastAccessTime) {
                Width = 175;
                Converter = EntryDataConverter.DateTime;
            }
        }

        public sealed class LastAccessTimeUtc : Definition<DateTime?> {
            public LastAccessTimeUtc() : base(i => i.LastAccessTimeUtc) {
                Width = 175;
                Converter = EntryDataConverter.DateTime;
            }
        }

        public sealed class LastWriteTime : Definition<DateTime?> {
            public LastWriteTime() : base(i => i.LastWriteTime) {
                Width = 175;
                Converter = EntryDataConverter.DateTime;
            }
        }

        public sealed class LastWriteTimeUtc : Definition<DateTime?> {
            public LastWriteTimeUtc() : base(i => i.LastWriteTimeUtc) {
                Width = 175;
                Converter = EntryDataConverter.DateTime;
            }
        }

        public sealed class Icon : SSHEntryData<object> {
            protected sealed override async Task<object> GetValue(SSHEntry entry, Action<object> progress, CancellationToken token) {
                var icon = default(object);
                var task = Service?.Work(entry.Info, set: value => icon = value, token);
                if (task == null) {
                    return null;
                }
                var work = await task.ConfigureAwait(false);
                if (work == false) {
                    return null;
                }
                return icon;
            }

            public ISSHEntryInfoIcon Service { get; set; }

            public sealed override Task<bool> SuggestKey(ICommandContext context, CancellationToken token) {
                return Task.FromResult(false);
            }
        }

        public abstract class Definition<T> : SSHEntryData<T> {
            protected Func<SSHFileInfo, T> Func { get; }

            protected Definition(Func<SSHFileInfo, T> func) {
                Func = func ?? throw new ArgumentNullException(nameof(func));
            }

            protected sealed override Task<T> GetValue(SSHEntry entry, Action<T> progress, CancellationToken token) {
                if (null == entry) throw new ArgumentNullException(nameof(entry));
                return Task.FromResult(Func(entry.Info));
            }
        }
    }

    internal abstract class SSHEntryData<T> : EntryDataDefinition<SSHEntry, T> {
        public sealed override string Group =>
            nameof(SSHEntryData);
    }
}
