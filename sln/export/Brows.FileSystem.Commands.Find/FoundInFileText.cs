using System;
using System.IO;

namespace Brows {
    using Commands;

    internal sealed class FoundInFileText : FoundInInfo {
        public string LineLabel => string.Format(Translation.Global.Value("FileSystem_Find_FoundInFileText_LineLabel"), Match.Line);
        public string LineText => $"{Match}".Trim();

        public FindMatch Match { get; }

        public FoundInFileText(FileInfo info, DirectoryInfo findRoot, FindMatch match) : base(info, findRoot) {
            Match = match ?? throw new ArgumentNullException(nameof(match));
        }
    }
}
