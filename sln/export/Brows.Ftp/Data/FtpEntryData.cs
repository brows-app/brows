using Brows.Exports;
using Brows.Url.Ftp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Data {
    internal static class FtpEntryData {
        public sealed class Extension : Definition<string> {
            public Extension() : base(e => e.Info.Extension) {
                Width = 50;
            }
        }

        public sealed class Name : Definition<string> {
            public Name() : base(e => e.Info.Name) {
                Width = 250;
            }
        }

        public sealed class Kind : Definition<FileProtocolEntryKind> {
            public Kind() : base(e => e.Kind) {
                Width = 75;
            }
        }

        public sealed class Target : Definition<string> {
            public Target() : base(e => e.Info.Target) {
                Width = 100;
            }
        }

        public sealed class Time : Definition<DateTime?> {
            public Time() : base(e => e.Info.Time) {
                Width = 175;
            }
        }

        public sealed class Icon : FtpEntryData<object> {
            protected sealed override async Task<object> GetValue(FtpEntry entry, Action<object> progress, CancellationToken token) {
                if (entry is null) {
                    return null;
                }
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

            public IFtpEntryIcon Service { get; set; }

            public sealed override Task<bool> SuggestKey(ICommandContext context, CancellationToken token) {
                return Task.FromResult(false);
            }
        }

        public abstract class Definition<T> : FtpEntryData<T> {
            protected Func<FtpEntry, T> Func { get; }

            protected Definition(Func<FtpEntry, T> func) {
                Func = func ?? throw new ArgumentNullException(nameof(func));
            }

            protected sealed override Task<T> GetValue(FtpEntry entry, Action<T> progress, CancellationToken cancellationToken) {
                return Task.FromResult(Func(entry));
            }
        }
    }

    internal abstract class FtpEntryData<T> : EntryDataDefinition<FtpEntry, T> {
        public sealed override string Group =>
            nameof(FtpEntryData);
    }
}
