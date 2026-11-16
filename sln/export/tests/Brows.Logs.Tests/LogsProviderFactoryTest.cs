using Brows.Composition;
using Brows.Panels;
using Brows.Providers;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class LogsProviderFactoryTest {
    /*
     * The factory needs an import agent, so the composition fills it in the way the
     * application does.
     */
    private static async Task<IProvider> CreateFor(string id) {
        var factory = new LogsProviderFactory();
        IProvider provider = null;
        await ImportSandbox.Enter([factory], async () => {
            provider = await ((IProviderFactory)factory).CreateFor(id, new TestPanel(), CancellationToken.None);
        });
        return provider;
    }

    [Test]
    public async Task CreateFor_LogsID_CreatesLogsProvider() {
        var provider = await CreateFor("Logs");
        try {
            Assert.That(provider, Is.TypeOf<LogsProvider>());
            Assert.That(provider.ID, Is.EqualTo("Logs"));
        }
        finally {
            provider?.Dispose();
        }
    }

    [Test]
    public async Task CreateFor_LogsID_ProvidesNoEntries() {
        var provider = await CreateFor("Logs");
        try {
            await provider.Refresh(CancellationToken.None);
            Assert.That(provider, Is.Not.InstanceOf<IEntryProvider>());
        }
        finally {
            provider?.Dispose();
        }
    }

    [TestCase("logs")]
    [TestCase("Drives")]
    [TestCase("")]
    public async Task CreateFor_OtherID_ReturnsNull(string id) {
        var provider = await CreateFor(id);
        Assert.That(provider, Is.Null);
    }
}
