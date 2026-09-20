using Brows.Git;

namespace Brows; 
internal sealed class GitRepoHead {
    public string Name { get; }
    public GitRepoComparison Status { get; }

    public GitRepoHead(string name, GitRepoComparison status) {
        Name = name;
        Status = status;
    }

    public override string ToString() {
        return Name;
    }
}
