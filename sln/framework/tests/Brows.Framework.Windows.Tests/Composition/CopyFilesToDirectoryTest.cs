using Brows.Operations;
using Brows.Win32;
using Brows.Win32.Win32FileOperations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Composition;

[TestFixture]
internal sealed class CopyFilesToDirectoryTest {
    [Test]
    public async Task Work_LazyFiles_EnumeratesOnce() {
        var count = 0;
        IEnumerable<string> Files() {
            count++;
            yield return @"C:\from\one.txt";
            yield return @"C:\from\two.txt";
        }

        var op = new Mock<IWin32FileOperation>();
        var copyFiles = default(List<CopyFile>);
        op.SetupSet(o => o.CopyFiles = It.IsAny<List<CopyFile>>()).Callback<List<CopyFile>>(value => copyFiles = value);
        op.Setup(o => o.Operate(It.IsAny<IOperationProgress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new Mock<IWin32InteropServices>();
        service.Setup(s => s.PathsAreEquivalent(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        service.Setup(s => s.FileOperation(It.IsAny<string>()))
            .Returns(op.Object);

        var work = new CopyFilesToDirectory { Service = service.Object };
        var result = await work.Work(Files(), @"C:\to", Mock.Of<IOperationProgress>(), CancellationToken.None);

        /*
         * The single materialized list must drive both the copy set and the
         * directory grouping, so a lazy input is enumerated exactly once.
         */
        Assert.That(result, Is.True);
        Assert.That(count, Is.EqualTo(1));
        Assert.That(copyFiles.Select(file => file.Path),
            Is.EqualTo(new[] { @"C:\from\one.txt", @"C:\from\two.txt" }));
        Assert.That(op.Object.PreserveFileExtensions, Is.False);
        Assert.That(op.Object.RenameOnCollision, Is.False);
    }
}
