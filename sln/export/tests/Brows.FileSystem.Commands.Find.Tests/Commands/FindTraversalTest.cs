using Brows.Matching;
using Brows.Support;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

[TestFixture]
public sealed class FindTraversalTest {
    private static async Task<List<FileSystemInfo>> Collect(
        IAsyncEnumerable<FileSystemInfo> enumerable,
        CancellationToken token = default) {
        var items = new List<FileSystemInfo>();
        await foreach (var item in enumerable.WithCancellation(token)) {
            items.Add(item);
        }
        return items;
    }

    private static IMatcher Matcher(string pattern) {
        return MatchAlgorithm.Create().Matcher(pattern);
    }

    private static string[] RelativePaths(DirectoryInfo root, IEnumerable<FileSystemInfo> items) {
        return items
            .Select(item => Path.GetRelativePath(root.FullName, item.FullName))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    [Test]
    public async Task Enumerate_FollowsExplicitRootJunction_AndPreservesLogicalPaths() {
        using var fixture = new TempDirectory();
        var target = fixture.CreateDirectory("target");
        fixture.CreateFile(Path.Combine("target", "top.txt"));
        fixture.CreateFile(Path.Combine("target", "nested", "deep.txt"));
        var junction = fixture.CreateJunction("selected-root", target);

        var realEntries = await Collect(
            FindTraversal.Enumerate(target, recurse: true, Array.Empty<IMatcher>(), default));
        var linkedEntries = await Collect(
            FindTraversal.Enumerate(junction, recurse: true, Array.Empty<IMatcher>(), default));

        Assert.That(RelativePaths(target, realEntries), Is.EqualTo(RelativePaths(junction, linkedEntries)));
        Assert.That(RelativePaths(junction, linkedEntries), Is.EquivalentTo(new[] {
            "top.txt",
            Path.Combine("nested", "deep.txt"),
            "nested"
        }));
        var allEntriesUseLogicalRoot = linkedEntries.All(item =>
            item.FullName.StartsWith(junction.FullName, StringComparison.OrdinalIgnoreCase));
        Assert.That(allEntriesUseLogicalRoot, Is.True);
    }

    [Test]
    public async Task Enumerate_TopDirectoryOnly_YieldsChildrenWithoutDescending() {
        using var fixture = new TempDirectory();
        var root = fixture.CreateDirectory("root");
        fixture.CreateFile(Path.Combine("root", "top.txt"));
        fixture.CreateFile(Path.Combine("root", "child", "nested.txt"));
        var junction = fixture.CreateJunction("selected-root", root);

        var rootItems = await Collect(FindTraversal.Enumerate(root, recurse: false, Array.Empty<IMatcher>(), default));
        var junctionItems = await Collect(
            FindTraversal.Enumerate(junction, recurse: false, Array.Empty<IMatcher>(), default));

        Assert.That(RelativePaths(root, rootItems), Is.EquivalentTo(new[] { "top.txt", "child" }));
        Assert.That(RelativePaths(junction, junctionItems), Is.EquivalentTo(RelativePaths(root, rootItems)));
    }

    [Test]
    public async Task Enumerate_YieldsButDoesNotTraverseChildJunctionToAncestor() {
        using var fixture = new TempDirectory();
        var root = fixture.CreateDirectory("root");
        fixture.CreateFile(Path.Combine("root", "top.txt"));
        fixture.CreateFile(Path.Combine("root", "child", "inner.txt"));
        fixture.CreateJunction(Path.Combine("root", "child", "ancestor"), root);

        var items = await Collect(FindTraversal.Enumerate(root, recurse: true, Array.Empty<IMatcher>(), default));
        var paths = RelativePaths(root, items);

        Assert.That(paths, Is.EquivalentTo(new[] {
            "top.txt",
            "child",
            Path.Combine("child", "inner.txt"),
            Path.Combine("child", "ancestor")
        }));
        Assert.That(paths.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(paths.Length));
    }

    [Test]
    public async Task Enumerate_ExcludedDirectory_IsPrunedWithItsContents() {
        using var fixture = new TempDirectory();
        var root = fixture.CreateDirectory("root");
        fixture.CreateFile(Path.Combine("root", "keep.txt"));
        fixture.CreateFile(Path.Combine("root", "skip", "hidden.txt"));

        var items = await Collect(FindTraversal.Enumerate(
            root,
            recurse: true,
            new[] { Matcher("*skip*") },
            default));

        Assert.That(RelativePaths(root, items), Is.EquivalentTo(new[] { "keep.txt" }));
    }

    [Test]
    public async Task Enumerate_ReportsChildDirectoryThatDisappearsBeforeDescent() {
        using var fixture = new TempDirectory();
        var root = fixture.CreateDirectory("root");
        var child = fixture.CreateDirectory(Path.Combine("root", "child"));
        fixture.CreateFile(Path.Combine("root", "keep.txt"));
        var errors = 0;
        var items = new List<FileSystemInfo>();
        var enumerable = FindTraversal.Enumerate(
            root,
            recurse: true,
            Array.Empty<IMatcher>(),
            () => Interlocked.Increment(ref errors),
            default);

        await foreach (var item in enumerable) {
            items.Add(item);
            if (item.FullName == child.FullName) {
                Directory.Delete(child.FullName);
            }
        }

        Assert.That(RelativePaths(root, items), Is.EquivalentTo(new[] { "child", "keep.txt" }));
        Assert.That(errors, Is.EqualTo(1));
    }

    [Test]
    public async Task Enumerate_IncludesHiddenAndSystemEntries() {
        using var fixture = new TempDirectory();
        var root = fixture.CreateDirectory("root");
        var hiddenFile = fixture.CreateFile(Path.Combine("root", "hidden.txt"));
        File.SetAttributes(hiddenFile.FullName, FileAttributes.Hidden | FileAttributes.System);
        var hiddenDirectory = fixture.CreateDirectory(Path.Combine("root", "hidden-directory"));
        File.SetAttributes(
            hiddenDirectory.FullName,
            FileAttributes.Directory | FileAttributes.Hidden | FileAttributes.System);
        fixture.CreateFile(Path.Combine("root", "hidden-directory", "nested.txt"));

        var items = await Collect(FindTraversal.Enumerate(root, recurse: true, Array.Empty<IMatcher>(), default));

        Assert.That(RelativePaths(root, items), Is.EquivalentTo(new[] {
            "hidden.txt",
            "hidden-directory",
            Path.Combine("hidden-directory", "nested.txt")
        }));
    }

    [Test]
    public async Task Enumerate_BrokenRootJunction_PropagatesDirectoryError() {
        using var fixture = new TempDirectory();
        var target = fixture.CreateDirectory("target");
        var junction = fixture.CreateJunction("broken-root", target);
        Directory.Delete(target.FullName);

        var exception = await Assert.CatchAsync<IOException>(async () => {
            await Collect(FindTraversal.Enumerate(junction, recurse: true, Array.Empty<IMatcher>(), default));
        });

        Assert.That(exception, Is.Not.Null);
    }

    [Test]
    public async Task Enumerate_MissingRoot_PropagatesDirectoryError() {
        using var fixture = new TempDirectory();
        var root = new DirectoryInfo(Path.Combine(fixture.Root.FullName, "missing"));

        var exception = await Assert.CatchAsync<IOException>(async () => {
            await Collect(FindTraversal.Enumerate(root, recurse: true, Array.Empty<IMatcher>(), default));
        });

        Assert.That(exception, Is.Not.Null);
    }

    [Test]
    public async Task Enumerate_CanceledToken_ThrowsBeforeOpeningRoot() {
        using var fixture = new TempDirectory();
        var root = new DirectoryInfo(Path.Combine(fixture.Root.FullName, "missing"));
        using var source = new CancellationTokenSource();
        source.Cancel();

        var exception = await Assert.CatchAsync<OperationCanceledException>(async () => {
            await Collect(FindTraversal.Enumerate(root, recurse: true, Array.Empty<IMatcher>(), source.Token));
        });

        Assert.That(exception, Is.Not.Null);
    }
}
