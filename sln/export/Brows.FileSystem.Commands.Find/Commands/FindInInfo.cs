using Domore.IO.Extensions;
using Domore.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Config;

    internal sealed class FindInInfo {
        private readonly IMatcher Matcher;

        private async Task<FoundInInfo[]> FindInFileText(FileInfo file, CancellationToken token) {
            if (file is null) throw new ArgumentNullException(nameof(file));
            var config = await Configure.File<FileSystemConfig>().Load(token);
            var options = Parameter.Decoder;
            var decoded = default(DecodedText);
            try {
                decoded = await file.DecodeText(default(DecodedTextDelegate), options, token);
            }
            catch (Exception ex) {
                return new[] { new FoundInFileError(file, Root, ex) };
            }
            if (decoded == null) return null;
            if (decoded.Success == false) {
                return null;
            }
            return await Task.Run(cancellationToken: token, function: () => {
                var text = decoded.Text();
                if (text != null && text.Length > 0) {
                    var matches = Matcher.Matches(text, out var matched);
                    if (matches) {
                        return FindMatch.In(text, matched).Select(m => new FoundInFileText(file, Root, m)).ToArray();
                    }
                }
                return null;
            });
        }

        private async Task<FoundInInfo[]> FindInFileName(FileInfo file, CancellationToken token) {
            if (null == file) throw new ArgumentNullException(nameof(file));
            return await Task.Run(cancellationToken: token, function: () => {
                if (Matcher.Matches(file.Name)) {
                    return new[] { new FoundInFileName(file, Root) };
                }
                return null;
            });
        }

        private async Task<FoundInInfo[]> FindInDirectoryName(DirectoryInfo directory, CancellationToken token) {
            if (null == directory) throw new ArgumentNullException(nameof(directory));
            return await Task.Run(cancellationToken: token, function: () => {
                if (Matcher.Matches(directory.Name)) {
                    return new[] { new FoundInDirectoryName(directory, Root) };
                }
                return null;
            });
        }

        public DirectoryInfo Root { get; }
        public FindParameter Parameter { get; }

        public FindInInfo(DirectoryInfo root, FindParameter parameter) {
            Root = root;
            Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
            Matcher = Parameter.PatternMatcher();
        }

        public IEnumerable<Task<FoundInInfo[]>> Tasks(FileSystemInfo info, CancellationToken token) {
            var find = Parameter.In;
            if (info is FileInfo file) {
                if (find.HasFlag(FindIn.FileName)) {
                    yield return FindInFileName(file, token);
                }
                if (find.HasFlag(FindIn.FileText)) {
                    yield return FindInFileText(file, token);
                }
            }
            else {
                if (find.HasFlag(FindIn.DirectoryName)) {
                    if (info is DirectoryInfo directory) {
                        yield return FindInDirectoryName(directory, token);
                    }
                }
            }
        }
    }
}
