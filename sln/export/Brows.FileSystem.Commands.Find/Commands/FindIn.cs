using Domore.Conf;
using Domore.Conf.Cli;
using System;

namespace Brows.Commands {
    [Flags]
    internal enum FindIn {
        [CliDisplay(include: false)]
        None = 0,

        [Conf("DirName")]
        DirectoryName = 1,
        FileName = 2,
        FileText = 4,

        [Conf("TopDirOnly")]
        TopDirectoryOnly = 8,

        [CliDisplay(include: false)]
        FolderName = DirectoryName
    }
}
