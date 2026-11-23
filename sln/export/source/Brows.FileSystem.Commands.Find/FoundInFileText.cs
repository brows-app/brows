using System.IO;

namespace Brows;

internal sealed class FoundInFileText : FoundInInfo {
    public string LineLabel => string.Format(
        Translation?.Value("FileSystem_Find_FoundInFileText_LineLabel") ?? "{0}", Match.Line + 1);
    public string LineText => $"{Match}".Trim();

    public FindMatch Match { get; }
    public ITranslation Translation { get; }

    public FoundInFileText(FileInfo info, DirectoryInfo findRoot, FindMatch match, ITranslation translation)
    : base(info, findRoot) {
        Match = match ?? throw new ArgumentNullException(nameof(match));
        Translation = translation;
    }
}
