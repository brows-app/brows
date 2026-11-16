using Brows.Composition;
using Brows.Panels;
using Brows.Providers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class MBoxProviderFactoryTest {
    private MBoxTestDirectory _Directory;

    [SetUp]
    public void SetUp() {
        _Directory = new MBoxTestDirectory();
    }

    [TearDown]
    public void TearDown() {
        _Directory.Dispose();
    }

    private static IExport[] Exports(MBoxProviderFactory factory) {
        return [
            factory,
            new Brows.Data.MBoxEntryData.Subject(),
            new Brows.Data.MBoxEntryData.From(),
            new Brows.Data.MBoxEntryData.Date(),
            new Brows.Data.MBoxEntryData.To(),
            new Brows.Data.MBoxEntryData.MessageId(),
            new Brows.Data.MBoxEntryData.Labels(),
            new Brows.Data.MBoxEntryData.ThreadId(),
            new Brows.Data.MBoxEntryData.Size(),
        ];
    }

    /*
     * The factory needs an import agent, and the only way to give it one is to let the
     * composition fill it in, so that the provider is created the way the application creates
     * it.
     */
    private static async Task<IProvider> CreateFor(string path) {
        var factory = new MBoxProviderFactory();
        IProvider provider = null;
        await ImportSandbox.Enter(Exports(factory), async () => {
            provider = await ((IProviderFactory)factory).CreateFor(path, new TestPanel(), CancellationToken.None);
        });
        return provider;
    }

    private string Mailbox(string name) {
        return MBoxTestFixture.WriteFile(
            _Directory.Directory,
            name,
            MBoxTestFixture.Mailbox(
                "First body.\n",
                new[] {
                    "Subject: First subject",
                    "From: sender@example.com",
                }));
    }

    [Test]
    public async Task CreateFor_CreatesTheProviderOfTheMailbox() {
        var path = Mailbox("inbox.mbox");
        var provider = await CreateFor(path);
        try {
            Assert.That(provider, Is.Not.Null);
            Assert.That(provider.ID, Is.EqualTo(Path.GetFullPath(path)));
        }
        finally {
            provider?.Dispose();
        }
    }

    [Test]
    public async Task CreateFor_CreatesTheProviderOfAMailboxWhoseExtensionIsNotLowercase() {
        var path = Mailbox("inbox.MBOX");
        var provider = await CreateFor(path);
        try {
            Assert.That(provider, Is.Not.Null);
        }
        finally {
            provider?.Dispose();
        }
    }

    [Test]
    public async Task CreateFor_SetsTheParentOfTheProviderToTheDirectoryOfTheMailbox() {
        var path = Mailbox("inbox.mbox");
        var provider = await CreateFor(path);
        try {
            Assert.That(provider.Parent, Is.EqualTo(_Directory.Directory.FullName));
        }
        finally {
            provider?.Dispose();
        }
    }

    [Test]
    public async Task CreateFor_CreatesNoProviderForAMailboxThatDoesNotExist() {
        var path = Path.Combine(_Directory.Directory.FullName, "missing.mbox");
        var provider = await CreateFor(path);
        Assert.That(provider, Is.Null);
    }

    [Test]
    public async Task CreateFor_CreatesNoProviderForACompressedMailbox() {
        var path = Mailbox("inbox.mbox.gz");
        var provider = await CreateFor(path);
        Assert.That(provider, Is.Null);
    }

    [Test]
    public async Task CreateFor_CreatesNoProviderForAFileThatIsNotAMailbox() {
        var path = Mailbox("inbox.txt");
        var provider = await CreateFor(path);
        Assert.That(provider, Is.Null);
    }

    [Test]
    public async Task CreateFor_CreatesNoProviderForAnEmptyId() {
        var provider = await CreateFor("   ");
        Assert.That(provider, Is.Null);
    }
}
