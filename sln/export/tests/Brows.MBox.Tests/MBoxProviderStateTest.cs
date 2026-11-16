using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class MBoxProviderStateTest {
    private MBoxTestDirectory _Directory;

    [SetUp]
    public void SetUp() {
        _Directory = new MBoxTestDirectory();
    }

    [TearDown]
    public void TearDown() {
        _Directory.Dispose();
    }

    private Task<MBoxProvider> Provider(string name, string text, MBoxConfig config = null) {
        var path = MBoxTestFixture.WriteFile(_Directory.Directory, name, text);
        return MBoxTestProvider.Create(path, config ?? new MBoxConfig());
    }

    private static string Mailbox(params string[] bodies) {
        return MBoxTestFixture.Mailbox(bodies, new[] {
            "Subject: A subject",
            "From: sender@example.com",
            "Date: Thu, 17 Sep 2015 17:54:37 +0000",
        });
    }

    [Test]
    public async Task Read_SetsTheStateOfTheProviderToEmptyWhenTheMailboxHoldsNoMessage() {
        var provider = await Provider("empty.mbox", "");
        try {
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Empty));
                Assert.That(provider.IndexedCount, Is.EqualTo(0));
                Assert.That(provider.Error, Is.Empty);
            }
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Read_SetsTheStateOfTheProviderToFailedWhenTheMailboxIsMalformed() {
        var provider = await Provider("invalid.mbox", "not a mailbox at all\n");
        try {
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Failed));
                Assert.That(provider.IndexedCount, Is.EqualTo(0));
                Assert.That(provider.Error, Is.Not.Empty);
            }
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Read_SetsTheStateOfTheProviderToFailedWhenTheMailboxIsMissing() {
        var path = MBoxTestFixture.WriteFile(
            _Directory.Directory,
            "missing.mbox",
            Mailbox("First body.\n"));
        var provider = await MBoxTestProvider.Create(path, new MBoxConfig());
        try {
            File.Delete(path);
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Failed));
                Assert.That(provider.Error, Is.Not.Empty);
            }
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Read_SetsTheStateOfTheProviderToReadyWhenTheMailboxIsValid() {
        var provider = await Provider("inbox.mbox", Mailbox("First body.\n"));
        try {
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Ready));
                Assert.That(provider.IndexedCount, Is.EqualTo(1));
                Assert.That(provider.Error, Is.Empty);
            }
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Read_SetsTheStateOfTheProviderToPartialWhenALimitStopsTheIndex() {
        var config = new MBoxConfig {
            MaxEntries = 1
        };
        var provider = await Provider("many.mbox", Mailbox("First body.\n", "Second body.\n"), config);
        try {
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Partial));
                Assert.That(provider.IndexedCount, Is.EqualTo(1));
            }
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Read_SetsTheStateOfTheProviderToCancelledWhenTheReadIsCancelled() {
        var provider = await Provider("inbox.mbox", Mailbox("First body.\n"));
        try {
            using var source = new CancellationTokenSource();
            source.Cancel();
            await MBoxTestProvider.Refresh(provider, source.Token);
            Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Cancelled));
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Read_FromPartialToReadyClearsTheStatusAndError() {
        var config = new MBoxConfig {
            MaxEntries = 1,
        };
        var provider = await Provider("many.mbox", Mailbox("First body.\n", "Second body.\n"), config);
        try {
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Partial));
            Assert.That(provider.Status, Is.Not.Empty);
            config.MaxEntries = 10;
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Ready));
                Assert.That(provider.Status, Is.Empty);
                Assert.That(provider.Error, Is.Empty);
                Assert.That(provider.Refreshing, Is.False);
            }
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Read_FromFailedToReadyClearsTheErrorAndStatus() {
        var provider = await Provider("inbox.mbox", Mailbox("First body.\n"));
        try {
            provider.IndexStageForTesting = _ => throw new IOException("Injected failure.");
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Failed));
            Assert.That(provider.Error, Is.Not.Empty);
            provider.IndexStageForTesting = null;
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Ready));
                Assert.That(provider.Status, Is.Empty);
                Assert.That(provider.Error, Is.Empty);
            }
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Read_FromCancelledToReadyClearsTheStatusAndError() {
        var provider = await Provider("inbox.mbox", Mailbox("First body.\n"));
        try {
            using (var source = new CancellationTokenSource()) {
                source.Cancel();
                await MBoxTestProvider.Refresh(provider, source.Token);
            }
            Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Cancelled));
            Assert.That(provider.Status, Is.Not.Empty);
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Ready));
                Assert.That(provider.Status, Is.Empty);
                Assert.That(provider.Error, Is.Empty);
            }
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Read_SetsAndClearsRefreshingAroundASignaledIndex() {
        var provider = await Provider("inbox.mbox", Mailbox("First body.\n"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.IndexGateForTesting = async token => {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        };
        try {
            var refresh = MBoxTestProvider.Refresh(provider, CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using (Assert.EnterMultipleScope()) {
                Assert.That(provider.Refreshing, Is.True);
                Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Loading));
                Assert.That(provider.Status, Is.Not.Empty);
                Assert.That(provider.Error, Is.Empty);
            }
            release.TrySetResult();
            await refresh;
            using (Assert.EnterMultipleScope()) {
                Assert.That(provider.Refreshing, Is.False);
                Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Ready));
                Assert.That(provider.Status, Is.Empty);
                Assert.That(provider.Error, Is.Empty);
            }
        }
        finally {
            release.TrySetResult();
            provider.Dispose();
        }
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void MaxActivePreviewDecodes_RejectsNonPositiveValues(int value) {
        Assert.That(() => new MBoxConfig { MaxActivePreviewDecodes = value }, Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}
