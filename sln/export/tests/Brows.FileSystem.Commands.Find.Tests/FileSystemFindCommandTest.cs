using Brows.Commands;
using Brows.Support;
using System.IO;
using System.Linq;

namespace Brows;

[TestFixture]
public sealed class FileSystemFindCommandTest {
    private static FileSystemFindResult Result(DirectoryInfo root, bool complete) {
        return new FileSystemFindResult(root, new Find.Parameter { Pattern = "*" }, "find", null) {
            Complete = complete
        };
    }

    [Test]
    public void PruneCompleted_KeepsNewestTwentyAndAllActiveResults() {
        using var fixture = new TempDirectory();
        var command = new FileSystemFindCommand();
        var newest = Enumerable.Range(0, 25)
            .Select(_ => Result(fixture.Root, complete: true))
            .ToArray();
        var activeFirst = Result(fixture.Root, complete: false);
        var activeLast = Result(fixture.Root, complete: false);
        command.List.Add(newest[0]);
        command.List.Add(activeFirst);
        for (var i = 1; i < newest.Length; i++) {
            command.List.Add(newest[i]);
        }
        command.List.Add(activeLast);

        command.PruneCompleted();

        Assert.That(command.List, Has.Count.EqualTo(22));
        Assert.That(command.List, Is.EquivalentTo(newest.Take(20).Concat(new[] { activeFirst, activeLast })));
    }
}
