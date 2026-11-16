using Brows.Operations;
using Brows.Panels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

[TestFixture]
internal sealed class FtpUrlTest {
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private Func<IOperationProgress, CancellationToken, Task<bool>> Operation;
    private string ProvidedID;

    private bool Work(string input) {
        var panel = new Mock<IPanel>();
        panel.Setup(item => item.Provide(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => ProvidedID = id)
            .ReturnsAsync(true);
        var active = panel.Object;
        var context = new Mock<ICommandContext>();
        context.Setup(item => item.HasInput(out input)).Returns(input != "");
        context.Setup(item => item.HasPanel(out active)).Returns(true);
        context.Setup(item => item.Operate(It.IsAny<Func<IOperationProgress, CancellationToken, Task<bool>>>()))
            .Callback<Func<IOperationProgress, CancellationToken, Task<bool>>>(work => Operation = work)
            .Returns(true);
        return ((ICommand)new FtpUrl()).ArbitraryWork(context.Object);
    }

    [SetUp]
    public void SetUp() {
        Operation = null;
        ProvidedID = null;
    }

    [TestCase("ftp://example.test/root/")]
    [TestCase("ftp://user@example.test:2121/")]
    [TestCase("ftps://example.test/root/")]
    [TestCase("FTPS://example.test/")]
    [TestCase("ftpes://example.test/root/")]
    public async Task ArbitraryWork_FtpUrl_ProvidesUrlInActivePanel(string input) {
        var worked = Work(input);

        Assert.That(worked, Is.True);
        Assert.That(Operation, Is.Not.Null);
        var provided = await Operation(Mock.Of<IOperationProgress>(), CancellationToken.None).WaitAsync(Timeout);
        Assert.Multiple(() => {
            Assert.That(provided, Is.True);
            Assert.That(ProvidedID, Is.EqualTo(new Uri(input).ToString()));
        });
    }

    [Test]
    public async Task ArbitraryWork_FtpUrlWithoutPath_ProvidesRootUrl() {
        var worked = Work("ftp://example.test");

        Assert.That(worked, Is.True);
        await Operation(Mock.Of<IOperationProgress>(), CancellationToken.None).WaitAsync(Timeout);
        Assert.That(ProvidedID, Is.EqualTo("ftp://example.test/"));
    }

    [TestCase("")]
    [TestCase("ftp")]
    [TestCase("ftp example.test")]
    [TestCase("example.test")]
    [TestCase("sftp://example.test/")]
    [TestCase("http://example.test/")]
    [TestCase(@"C:\Users")]
    public void ArbitraryWork_NotFtpUrl_ReturnsFalse(string input) {
        var worked = Work(input);

        Assert.Multiple(() => {
            Assert.That(worked, Is.False);
            Assert.That(Operation, Is.Null);
        });
    }

    [Test]
    public void ArbitraryWork_NullContext_ReturnsFalse() {
        var worked = ((ICommand)new FtpUrl()).ArbitraryWork(null);

        Assert.That(worked, Is.False);
    }
}
