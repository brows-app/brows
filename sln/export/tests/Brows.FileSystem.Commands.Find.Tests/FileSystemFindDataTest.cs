using Brows.Commands;
using Brows.Support;
using System.Collections.Generic;
using System.IO;

namespace Brows;

[TestFixture]
public sealed class FileSystemFindDataTest {
    private static FileSystemFindResult Result(DirectoryInfo root, bool complete) {
        return new FileSystemFindResult(root, new Find.Parameter { Pattern = "*" }, "find", null) {
            Complete = complete
        };
    }

    [Test]
    public void Clear_RemovesCompletedResultsAndKeepsCurrentRunningResult() {
        using var fixture = new TempDirectory();
        var newestComplete = Result(fixture.Root, complete: true);
        var active = Result(fixture.Root, complete: false);
        var failed = Result(fixture.Root, complete: true);
        failed.Exception = new IOException("The file could not be read.");
        var canceled = Result(fixture.Root, complete: true);
        canceled.Canceled = true;
        IList<FileSystemFindResult> list = new List<FileSystemFindResult> {
            newestComplete,
            active,
            failed,
            canceled
        };
        var data = new FileSystemFindData(Mock.Of<ICommand>(), Mock.Of<ICommandContext>(), 1, list);

        var remaining = (FileSystemFindData)data.Clear();

        Assert.That(list, Is.EqualTo(new[] { active }));
        Assert.That(remaining, Is.Not.Null);
        Assert.That(remaining.Current, Is.SameAs(active));
    }

    [Test]
    public void Clear_KeepsAnAllRunningHistory() {
        using var fixture = new TempDirectory();
        var first = Result(fixture.Root, complete: false);
        var second = Result(fixture.Root, complete: false);
        IList<FileSystemFindResult> list = new List<FileSystemFindResult> { first, second };
        var data = new FileSystemFindData(Mock.Of<ICommand>(), Mock.Of<ICommandContext>(), 1, list);

        var remaining = data.Clear();

        Assert.That(list, Is.EqualTo(new[] { first, second }));
        Assert.That(remaining.Current, Is.SameAs(second));
    }

    [Test]
    public void Clear_ReturnsNullWhenNoRunningResultsRemain() {
        using var fixture = new TempDirectory();
        IList<FileSystemFindResult> list = new List<FileSystemFindResult> {
            Result(fixture.Root, complete: true),
            Result(fixture.Root, complete: true)
        };
        var data = new FileSystemFindData(Mock.Of<ICommand>(), Mock.Of<ICommandContext>(), 0, list);

        var remaining = data.Clear();

        Assert.That(list, Is.Empty);
        Assert.That(remaining, Is.Null);
    }

    [Test]
    public void PrunedHistory_PreservesSelectionAndRemovesByIdentity() {
        using var fixture = new TempDirectory();
        var command = new FileSystemFindCommand();
        var completed = new List<FileSystemFindResult>();
        for (var i = 0; i < 21; i++) {
            var result = Result(fixture.Root, complete: true);
            completed.Add(result);
            command.List.Add(result);
        }
        var active = Result(fixture.Root, complete: false);
        command.List.Add(active);
        var data = new FileSystemFindData(
            Mock.Of<ICommand>(),
            Mock.Of<ICommandContext>(),
            index: 21,
            list: command.List);

        var newlyCompleted = Result(fixture.Root, complete: true);
        command.List.Insert(0, newlyCompleted);
        command.PruneCompleted();

        Assert.That(data.Current, Is.SameAs(active));
        Assert.That(command.List, Has.Count.EqualTo(21));
        Assert.That(command.List[0], Is.SameAs(newlyCompleted));

        var previous = (FileSystemFindData)data.Previous();
        Assert.That(previous.Current, Is.SameAs(completed[18]));
        var next = (FileSystemFindData)previous.Next();
        Assert.That(next.Current, Is.SameAs(active));

        var remaining = (FileSystemFindData)data.Remove();

        Assert.That(command.List, Has.Count.EqualTo(20));
        Assert.That(command.List, Does.Not.Contain(active));
        Assert.That(remaining.Current, Is.SameAs(completed[18]));
    }

    [Test]
    public void PrunedCurrentResult_NavigatesToAdjacentSurvivingResults() {
        using var fixture = new TempDirectory();
        var newer = Result(fixture.Root, complete: true);
        var selected = Result(fixture.Root, complete: true);
        var older = Result(fixture.Root, complete: true);
        IList<FileSystemFindResult> list = new List<FileSystemFindResult> { newer, selected, older };
        var data = new FileSystemFindData(Mock.Of<ICommand>(), Mock.Of<ICommandContext>(), 1, list);
        list.Remove(selected);

        Assert.That(data.Next().Current, Is.SameAs(older));
        Assert.That(data.Previous().Current, Is.SameAs(newer));
        Assert.That(data.Current, Is.SameAs(selected));
    }

    [Test]
    public void EmptyHistory_NavigationDoesNotRecreateAnEmptyResultView() {
        using var fixture = new TempDirectory();
        IList<FileSystemFindResult> list = new List<FileSystemFindResult> { Result(fixture.Root, complete: true) };
        var data = new FileSystemFindData(Mock.Of<ICommand>(), Mock.Of<ICommandContext>(), 0, list);
        list.Clear();

        Assert.That(data.Next(), Is.Null);
        Assert.That(data.Previous(), Is.Null);
        Assert.That(data.Remove(), Is.Null);
    }

    [Test]
    public void PrunedCurrentResult_RemoveDoesNotRemoveAReplacement() {
        using var fixture = new TempDirectory();
        var pruned = Result(fixture.Root, complete: true);
        var retained = Result(fixture.Root, complete: true);
        IList<FileSystemFindResult> list = new List<FileSystemFindResult> {
            pruned,
            retained
        };
        var data = new FileSystemFindData(
            Mock.Of<ICommand>(),
            Mock.Of<ICommandContext>(),
            index: 0,
            list: list);
        list.RemoveAt(0);

        var next = data.Remove();

        Assert.That(data.Current, Is.SameAs(pruned));
        Assert.That(list, Is.EqualTo(new[] { retained }));
        Assert.That(next.Current, Is.SameAs(retained));
    }
}
