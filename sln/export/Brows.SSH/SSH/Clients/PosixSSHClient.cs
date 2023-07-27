using Domore.Conf;
using Domore.Conf.Extensions;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using PATH = System.IO.Path;

namespace Brows.SSH.Clients {
    internal sealed class PosixSSHClient : SSHClient {
        public sealed override async IAsyncEnumerable<SSHEntryInfo> List(string path, [EnumeratorCancellation] CancellationToken token) {
            var newL = EntryInfo.NewLin;
            var frmt = EntryInfo.Format;
            var outp = SSH(@$"cd ""{path}"" && stat --printf='{frmt}{newL}*{newL}' $(ls -a)", token);
            var bldr = default(StringBuilder);
            await foreach (var line in outp) {
                if (line.Kind == SSHClientOutputKind.Output) {
                    var content = line.Content?.Trim();
                    if (content == "*") {
                        var conf = bldr?.ToString();
                        if (conf?.Length > 0) {
                            var info = new EntryInfo(path).ConfFrom(conf, key: "");
                            if (info.Name != null && info.Name != "." && info.Name != "..") {
                                yield return info;
                            }
                        }
                        bldr = null;
                        continue;
                    }
                    bldr ??= new StringBuilder();
                    bldr.AppendLine(content);
                }
            }
        }

        private sealed class EntryInfo : SSHEntryInfo {
            public const string NewLin = "\\n";
            public const string Format =
                "n1=%n" + NewLin +
                "N2=%N" + NewLin +
                "F=%F" + NewLin +
                "h=%h" + NewLin +
                "s=%s" + NewLin +
                "U=%U" + NewLin +
                "G=%G" + NewLin +
                "A=%A" + NewLin +
                "W=%W" + NewLin +
                "X=%X" + NewLin +
                "Y=%Y" + NewLin +
                "Z=%Z" + NewLin +
                "o=%o";

            [Conf("N1")] public string FileName { get; set; }
            [Conf("N2")] public string QuotedFileNameWithDereference { get; set; }
            [Conf("F")] public string FileType { get; set; }
            [Conf("h")] public long NumberOfHardLinks { get; set; }
            [Conf("s")] public long TotalSizeInBytes { get; set; }
            [Conf("U")] public string UserNameOfOwner { get; set; }
            [Conf("G")] public string GroupNameOfOwner { get; set; }
            [Conf("A")] public string AccessRights { get; set; }
            [Conf("W")] public long TimeOfFileBirth { get; set; }
            [Conf("X")] public long TimeOfLastAccess { get; set; }
            [Conf("Y")] public long TimeOfLastDataModification { get; set; }
            [Conf("Z")] public long TimeOfLastStatusChange { get; set; }
            [Conf("o")] public long OptimalIOTransferSizeHint { get; set; }

            public string Parent { get; }

            public EntryInfo(string parent) {
                Parent = parent;
            }

            public sealed override long? Length =>
                TotalSizeInBytes;

            public sealed override string LinkTarget =>
                _LinkTarget ?? (
                _LinkTarget = new Func<string>(() => {
                    var quotedFileName = $"'{FileName}'";
                    var quotedFileNameWithDeref = QuotedFileNameWithDereference;
                    if (quotedFileNameWithDeref == quotedFileName) {
                        return null;
                    }
                    var pre = $"{quotedFileName} -> '";
                    var linkTarget = quotedFileNameWithDeref.Substring(pre.Length, quotedFileNameWithDeref.Length - pre.Length - 1);
                    return linkTarget;
                })());
            private string _LinkTarget;

            public sealed override string Name =>
                _Name ?? (
                _Name = FileName);
            private string _Name;

            public sealed override string Path =>
                _Path ?? (
                _Path = string.Concat(Parent.TrimEnd('/'), "/", Name));
            private string _Path;

            public sealed override SSHEntryKind Kind =>
                _Kind ?? (
                _Kind =
                    "directory".Equals(FileType, StringComparison.OrdinalIgnoreCase) ? SSHEntryKind.Directory :
                    "regular file".Equals(FileType, StringComparison.OrdinalIgnoreCase) ? SSHEntryKind.File :
                    SSHEntryKind.Unknown).Value;
            private SSHEntryKind? _Kind;

            public sealed override string Extension =>
                _Extension ?? (
                _Extension = Kind == SSHEntryKind.File
                    ? PATH.GetExtension(Name)
                    : "");
            private string _Extension;

            public sealed override string OwnedByUser =>
                UserNameOfOwner;

            public override string OwnedByGroup =>
                GroupNameOfOwner;

            public override string Permissions =>
                AccessRights;

            public sealed override DateTime? CreationTimeUtc =>
                _CreationTimeUtc ?? (
                _CreationTimeUtc = TimeOfFileBirth == 0 ? null : DateTime.UnixEpoch.AddSeconds(TimeOfFileBirth));
            private DateTime? _CreationTimeUtc;

            public sealed override DateTime? CreationTime =>
                _CreationTime ?? (
                _CreationTime = CreationTimeUtc?.ToLocalTime());
            private DateTime? _CreationTime;

            public sealed override DateTime? LastWriteTimeUtc =>
                _LastWriteTimeUtc ?? (
                _LastWriteTimeUtc = DateTime.UnixEpoch.AddSeconds(TimeOfLastDataModification));
            private DateTime? _LastWriteTimeUtc;

            public sealed override DateTime? LastWriteTime =>
                _LastWriteTime ?? (
                _LastWriteTime = LastWriteTimeUtc?.ToLocalTime());
            private DateTime? _LastWriteTime;

            public sealed override DateTime? LastAccessTimeUtc =>
                _LastAccessTimeUtc ?? (
                _LastAccessTimeUtc = DateTime.UnixEpoch.AddSeconds(TimeOfLastAccess));
            private DateTime? _LastAccessTimeUtc;

            public sealed override DateTime? LastAccessTime =>
                _LastAccessTime ?? (
                _LastAccessTime = LastAccessTimeUtc?.ToLocalTime());
            private DateTime? _LastAccessTime;
        }
    }
}
