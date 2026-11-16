using Brows.SFTP.Native;
using Brows.Data;
using Brows.Exports;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace Brows.Data;

internal static class SftpEntryData {
    private const uint SizeFlag = 0x1;
    private const uint OwnerFlag = 0x2;
    private const uint PermissionsFlag = 0x4;
    private const uint TimesFlag = 0x8;

    public sealed class Name : Definition<string> {
        public Name() : base(entry => entry.Name) {
            Width = 250;
        }
    }

    public sealed class Kind : Definition<FileProtocolEntryKind> {
        public Kind() : base(entry => entry.Kind) {
            Width = 75;
        }
    }

    public sealed class Icon : SftpEntryData<object> {
        protected sealed override async Task<object> GetValue(SftpEntry entry, Action<object> progress, CancellationToken token) {
            if (entry is null) {
                return null;
            }
            var icon = default(object);
            var task = Service?.Work(entry.Kind, entry.Extension, set: value => icon = value, token);
            if (task is null || !await task) {
                return null;
            }
            return icon;
        }

        public ISftpEntryIcon Service { get; set; }
    }

    public sealed class Length : Definition<long?> {
        public Length() : base(entry => (entry.Info.Flags & SizeFlag) == 0 || entry.Info.Size > long.MaxValue
            ? null
            : (long)entry.Info.Size) {
            Width = 100;
            Converter = EntryDataConverter.FileSystemSize;
            Alignment = EntryDataAlignment.Right;
        }
    }

    public sealed class Permissions : Definition<string> {
        public Permissions() : base(entry => (entry.Info.Flags & PermissionsFlag) == 0
            ? null
            : Convert.ToString(entry.Info.Permissions & 0xFFF, 8)) {
            Width = 110;
        }
    }

    public sealed class Owner : Definition<string> {
        public Owner() : base(entry => (entry.Info.Flags & OwnerFlag) == 0
            ? null
            : entry.Info.Uid.ToString(CultureInfo.InvariantCulture)) {
            Width = 100;
        }
    }

    public sealed class Group : Definition<string> {
        public Group() : base(entry => (entry.Info.Flags & OwnerFlag) == 0
            ? null
            : entry.Info.Gid.ToString(CultureInfo.InvariantCulture)) {
            Width = 100;
        }
    }

    public sealed class LastAccessTime : Definition<DateTime?> {
        public LastAccessTime() : base(entry => (entry.Info.Flags & TimesFlag) == 0
            ? null
            : DateTimeOffset.FromUnixTimeSeconds(entry.Info.Atime).LocalDateTime) {
            Width = 175;
            Converter = EntryDataConverter.DateTime;
        }
    }

    public sealed class LastWriteTime : Definition<DateTime?> {
        public LastWriteTime() : base(entry => (entry.Info.Flags & TimesFlag) == 0
            ? null
            : DateTimeOffset.FromUnixTimeSeconds(entry.Info.Mtime).LocalDateTime) {
            Width = 175;
            Converter = EntryDataConverter.DateTime;
        }
    }

    public sealed class LastWriteTimeUtc : Definition<DateTime?> {
        public LastWriteTimeUtc() : base(entry => (entry.Info.Flags & TimesFlag) == 0
            ? null
            : DateTimeOffset.FromUnixTimeSeconds(entry.Info.Mtime).UtcDateTime) {
            Width = 175;
            Converter = EntryDataConverter.DateTime;
        }
    }

    internal abstract class Definition<T> : EntryDataExport<SftpEntry, T> {
        private Func<SftpEntry, T> Value { get; }
        public sealed override string Group => nameof(SftpEntryData);

        protected Definition(Func<SftpEntry, T> value) {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        protected sealed override Task<T> GetValue(SftpEntry entry, Action<T> progress, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(entry);
            return Task.FromResult(Value(entry));
        }
    }
}

internal abstract class SftpEntryData<T> : EntryDataExport<SftpEntry, T> {
    public sealed override string Group => nameof(SftpEntryData);
}
