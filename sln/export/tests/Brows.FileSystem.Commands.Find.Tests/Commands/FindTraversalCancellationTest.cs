using Brows.Matching;
using Brows.Support;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

[TestFixture]
internal sealed class FindTraversalCancellationTest {
    [Test]
    public async Task Enumerate_CancellationInsideSubdirectoryReleasesTraversal() {
        using var fixture = new TempDirectory();
        var child = fixture.CreateDirectory("child");
        for (var i = 0; i < 100; i++) {
            fixture.CreateFile(Path.Combine("child", $"{i}.txt"));
        }
        using var cancellation = new CancellationTokenSource();
        await using (var enumerator = FindTraversal.Enumerate(
            fixture.Root, true, Array.Empty<IMatcher>(), cancellation.Token).GetAsyncEnumerator()) {
            Assert.That(await enumerator.MoveNextAsync(), Is.True);
            Assert.That(await enumerator.MoveNextAsync(), Is.True);
            Assert.That(enumerator.Current, Is.TypeOf<FileInfo>());
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => {
                var moved = await enumerator.MoveNextAsync();
                Assert.That(moved, Is.False);
            });
        }

        var renamed = Path.Combine(fixture.Root.FullName, "renamed");
        Directory.Move(child.FullName, renamed);
        Directory.Move(renamed, child.FullName);
        using var exclusive = File.Open(Path.Combine(child.FullName, "0.txt"),
            FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.That(exclusive.CanRead, Is.True);
    }
}
