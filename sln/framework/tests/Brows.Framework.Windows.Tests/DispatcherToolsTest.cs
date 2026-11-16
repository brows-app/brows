using System.Windows.Threading;

namespace Brows;

[TestFixture]
internal sealed class DispatcherToolsTest {
    [Test]
    public void Run_Worker_IsBackground() {
        var isBackground = false;

        DispatcherTools.Run(dispatcher => {
            isBackground = Thread.CurrentThread.IsBackground;
        });

        Assert.That(isBackground, Is.True);
    }

    [Test]
    public void Run_BodyCompletes_ShutsDownDispatcher() {
        Dispatcher dispatcher = null;

        DispatcherTools.Run(current => {
            dispatcher = current;
        });

        Assert.That(dispatcher.HasShutdownFinished, Is.True);
    }

    [Test]
    public void Run_BodyThrows_PropagatesOriginalExceptionAndShutsDownDispatcher() {
        var error = new InvalidOperationException("The dispatcher body failed.");
        Dispatcher dispatcher = null;

        var actual = Assert.Throws<InvalidOperationException>(() => DispatcherTools.Run(current => {
            dispatcher = current;
            throw error;
        }));

        Assert.That(actual, Is.SameAs(error));

        Assert.That(dispatcher.HasShutdownFinished, Is.True);
    }
}
