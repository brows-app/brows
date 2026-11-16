using Brows.Entries;
using Brows.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class MBoxProviderTest {
    private MBoxTestDirectory _Directory;
    private string _Path;

    [SetUp]
    public void SetUp() {
        _Directory = new MBoxTestDirectory();
        _Path = MBoxTestFixture.WriteFile(
            _Directory.Directory,
            "inbox.mbox",
            MBoxTestFixture.Mailbox(
                "First body.\n",
                new[] {
                    "Subject: First subject",
                    "From: sender@example.com",
                    "Date: Thu, 17 Sep 2015 17:54:37 +0000",
                }));
    }

    [TearDown]
    public void TearDown() {
        _Directory.Dispose();
    }

    private Task<MBoxProvider> Provider() {
        return MBoxTestProvider.Create(_Path, new MBoxConfig());
    }

    private static async Task<List<MBoxEntry>> Read(MBoxProvider provider) {
        await MBoxTestProvider.Refresh(provider, CancellationToken.None);
        return provider.Entries.ToList();
    }

    [Test]
    public async Task Begin_ReadsTheMessagesOfTheMailbox() {
        var provider = await Provider();
        try {
            var entries = await Read(provider);
            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].Subject, Is.EqualTo("First subject"));
            Assert.That(entries[0].From, Is.EqualTo("sender@example.com"));
            Assert.That(entries[0].Date, Is.EqualTo(new DateTimeOffset(2015, 9, 17, 17, 54, 37, TimeSpan.Zero)));
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Begin_SetsTheStateOfTheProviderToReady() {
        var provider = await Provider();
        try {
            await Read(provider);
            Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Ready));
            Assert.That(provider.IndexedCount, Is.EqualTo(1));
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Begin_SetsTheStateOfTheProviderToEmptyWhenTheMailboxHoldsNoMessage() {
        var path = MBoxTestFixture.WriteFile(_Directory.Directory, "empty.mbox", "");
        var provider = await MBoxTestProvider.Create(path, new MBoxConfig());
        try {
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Empty));
            Assert.That(provider.IndexedCount, Is.EqualTo(0));
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Begin_SetsTheStateOfTheProviderToFailedWhenTheMailboxIsNotValid() {
        var path = MBoxTestFixture.WriteFile(_Directory.Directory, "invalid.mbox", "not a mailbox at all\n");
        var provider = await MBoxTestProvider.Create(path, new MBoxConfig());
        try {
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Failed));
            Assert.That(provider.Error, Is.Not.Empty);
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Begin_ShowsTheMessagesInTheOrderOfTheMailbox() {
        var path = MBoxTestFixture.WriteFile(
            _Directory.Directory,
            "many.mbox",
            MBoxTestFixture.Mailbox(
                new[] { "First body.\n", "Second body.\n", "Third body.\n" },
                new[] {
                    "Subject: Shared subject",
                    "From: sender@example.com",
                }));
        var provider = await MBoxTestProvider.Create(path, new MBoxConfig());
        try {
            var entries = await Read(provider);
            Assert.That(entries.Count, Is.EqualTo(3));
            Assert.That(entries.Select(entry => entry.Record.Offset), Is.Ordered);
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Begin_KeepsThePreviousMessagesWhenTheReadFails() {
        var provider = await Provider();
        try {
            var entries = await Read(provider);
            var generation = provider.ActiveGenerationId;
            provider.IndexStageForTesting = _ => throw new IOException("Injected index failure.");
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            var preview = await provider.Preview(entries[0], CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Failed));
                Assert.That(provider.Entries, Is.EqualTo(entries));
                Assert.That(provider.ActiveGenerationId, Is.EqualTo(generation));
                Assert.That(preview.Text, Is.EqualTo("First body.\n"));
            }
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Refresh_PublicationFailureDuringRemovalRestoresThePreviousGeneration() {
        var provider = await Provider();
        try {
            var entries = await Read(provider);
            var entry = entries[0];
            var observation = (IEntryObservation)((IEntryProvider)provider).Observation;
            var generation = provider.ActiveGenerationId;
            entry.Select = true;
            EventHandler failure = (_, _) => throw new IOException("Injected selection notification failure.");
            observation.SelectedChanged += failure;

            await MBoxTestProvider.Refresh(provider, CancellationToken.None);

            observation.SelectedChanged -= failure;
            var preview = await provider.Preview(entry, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Failed));
                Assert.That(provider.Entries, Is.EqualTo(entries));
                Assert.That(observation.Observed, Is.EqualTo(entries));
                Assert.That(observation.Selected, Does.Contain(entry));
                Assert.That(provider.ActiveGenerationId, Is.EqualTo(generation));
                Assert.That(preview.Text, Is.EqualTo("First body.\n"));
            }
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Refresh_PublicationFailureDuringAdditionRestoresThePreviousGeneration() {
        var provider = await Provider();
        try {
            var entries = await Read(provider);
            var observation = (IEntryObservation)((IEntryProvider)provider).Observation;
            var generation = provider.ActiveGenerationId;
            var notifications = 0;
            var restoredRowsUseThePreviousGeneration = false;
            EventHandler failure = (_, _) => {
                notifications++;
                if (notifications == 2) {
                    throw new IOException("Injected observation notification failure.");
                }
                if (notifications == 4) {
                    restoredRowsUseThePreviousGeneration =
                        provider.ActiveGenerationId == generation &&
                        provider.Entries.Count == 1 &&
                        ReferenceEquals(provider.Entries[0], entries[0]);
                }
            };
            observation.ObservedChanged += failure;

            await MBoxTestProvider.Refresh(provider, CancellationToken.None);

            observation.ObservedChanged -= failure;
            var preview = await provider.Preview(entries[0], CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(notifications, Is.GreaterThanOrEqualTo(2));
                Assert.That(restoredRowsUseThePreviousGeneration, Is.True);
                Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Failed));
                Assert.That(provider.Entries, Is.EqualTo(entries));
                Assert.That(observation.Observed, Is.EqualTo(entries));
                Assert.That(provider.ActiveGenerationId, Is.EqualTo(generation));
                Assert.That(preview.Text, Is.EqualTo("First body.\n"));
            }
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Begin_ReadsATakeoutShapedMailboxThroughTheProviderAndPreservesExtraction() {
        var message =
            "From: =?UTF-8?Q?Jos=C3=A9_Example?= <jose@example.com>\r\n" +
            "Subject: =?UTF-8?Q?Takeout_Caf=C3=A9?=\r\n" +
            "X-Gmail-Labels: Inbox, Important\r\n" +
            "X-GM-THRID: 1234567890\r\n" +
            "Content-Type: multipart/mixed;\r\n" +
            " boundary=\"outer\"\r\n" +
            "\r\n" +
            "--outer\r\n" +
            "Content-Type: multipart/alternative;\r\n" +
            " boundary=\"alternative\"\r\n" +
            "\r\n" +
            "--alternative\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            "\r\n" +
            "Takeout plain text.\r\n" +
            "--alternative\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n" +
            "\r\n" +
            "<p>Takeout HTML.</p>\r\n" +
            "--alternative--\r\n" +
            "--outer\r\n" +
            "Content-Type: application/octet-stream\r\n" +
            "Content-Disposition: attachment;\r\n" +
            " filename=\"image.bin\"\r\n" +
            "Content-Transfer-Encoding: base64\r\n" +
            "\r\n" +
            "QVRUQUNITUVOVC1TRUNSRVQ=\r\n" +
            "--outer--\r\n";
        var secondMessage =
            "From: second@example.com\r\n" +
            "Subject: Second message\r\n" +
            "\r\n" +
            "Second body.\r\n";
        var text = MBoxTestFixture.Envelope + "\r\n" +
                   message + "\r\n" +
                   "From second@example.com Thu Sep 17 18:09:45 +0000 2015\r\n" +
                   secondMessage;
        var path = MBoxTestFixture.WriteFile(_Directory.Directory, "takeout.mbox", text);
        var provider = await MBoxTestProvider.Create(path, new MBoxConfig());
        var extractedPath = Path.Combine(_Directory.Directory.FullName, "takeout.eml");
        try {
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            Assert.That(provider.Entries, Has.Count.EqualTo(2));
            var entry = provider.Entries[0];
            var preview = await provider.Preview(entry, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(entry.Subject, Is.EqualTo("Takeout Café"));
                Assert.That(entry.From, Is.EqualTo("José Example <jose@example.com>"));
                Assert.That(entry.Labels, Is.EqualTo("Inbox, Important"));
                Assert.That(entry.ThreadId, Is.EqualTo("1234567890"));
                Assert.That(preview.Text, Is.EqualTo("Takeout plain text.\n"));
                Assert.That(preview.Html, Is.EqualTo("<p>Takeout HTML.</p>\n"));
                Assert.That(preview.Text, Does.Not.Contain("ATTACHMENT-SECRET"));
                Assert.That(preview.Html, Does.Not.Contain("ATTACHMENT-SECRET"));
            }
            MBoxExtractor.Extract(entry, extractedPath, CancellationToken.None);
            Assert.That(File.ReadAllText(extractedPath), Is.EqualTo(message));
        }
        finally {
            provider.Dispose();
        }
    }

    [TestCase(MBoxProvider.IndexStage.Scan)]
    [TestCase(MBoxProvider.IndexStage.ReadHeaders)]
    [TestCase(MBoxProvider.IndexStage.Metadata)]
    public async Task Refresh_IndexFailureDisposesTheUnpublishedCandidate(MBoxProvider.IndexStage failingStage) {
        var provider = await Provider();
        provider.IndexStageForTesting = stage => {
            if (stage == failingStage) {
                throw new IOException($"Injected {stage} failure.");
            }
        };
        await MBoxTestProvider.Refresh(provider, CancellationToken.None);
        using (Assert.EnterMultipleScope()) {
            Assert.That(provider.State, Is.EqualTo(MBoxProviderState.Failed));
            Assert.That(provider.Entries, Is.Empty);
        }
        provider.Dispose();
        using var stream = new FileStream(_Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.That(stream.CanRead, Is.True);
    }

    [Test]
    public async Task Parent_IsTheDirectoryThatHoldsTheMailbox() {
        var provider = await Provider();
        try {
            Assert.That(provider.Parent, Is.EqualTo(_Directory.Directory.FullName));
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task ID_IsThePathOfTheMailbox() {
        var provider = await Provider();
        try {
            Assert.That(provider.ID, Is.EqualTo(_Path));
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Preview_ReadsTheBodyOfTheMessage() {
        var provider = await Provider();
        try {
            var entries = await Read(provider);
            var preview = await provider.Preview(entries[0], CancellationToken.None);
            Assert.That(preview.Text, Is.EqualTo("First body.\n"));
            Assert.That(preview.State, Is.EqualTo(Brows.MBox.Format.MBoxBodyState.Complete));
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Preview_ReturnsTheSameResultForTheSecondRead() {
        var provider = await Provider();
        try {
            var entries = await Read(provider);
            var first = await provider.Preview(entries[0], CancellationToken.None);
            var second = await provider.Preview(entries[0], CancellationToken.None);
            Assert.That(second, Is.SameAs(first));
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Preview_UsesTheLiveDecodeLimitForAlreadyQueuedRequests() {
        var config = new MBoxConfig {
            MaxActivePreviewDecodes = 1,
        };
        var provider = await MBoxTestProvider.Create(_Path, config);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var twoDecodesEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var counterLocker = new Lock();
        var active = 0;
        var maximumActive = 0;
        try {
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            var entry = provider.Entries[0];
            provider.DecodeGateForTesting = async token => {
                var count = Interlocked.Increment(ref active);
                lock (counterLocker) {
                    maximumActive = Math.Max(count, maximumActive);
                }
                if (count == 2) {
                    twoDecodesEntered.TrySetResult();
                }
                try {
                    await release.Task.WaitAsync(token);
                }
                finally {
                    Interlocked.Decrement(ref active);
                }
            };
            var first = provider.Preview(entry, CancellationToken.None);
            var second = provider.Preview(entry, CancellationToken.None);
            config.MaxActivePreviewDecodes = 2;
            await twoDecodesEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(maximumActive, Is.EqualTo(2));
            release.TrySetResult();
            await Task.WhenAll(first, second);
        }
        finally {
            release.TrySetResult();
            provider.Dispose();
        }
    }

    [Test]
    public async Task Preview_CancellationAndEnd_ReleaseRequestsQueuedBehindTheDecodeLimit() {
        var config = new MBoxConfig {
            MaxActivePreviewDecodes = 1,
        };
        var provider = await MBoxTestProvider.Create(_Path, config);
        var gateEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var firstToken = new CancellationTokenSource();
        try {
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            var entry = provider.Entries[0];
            provider.DecodeGateForTesting = async token => {
                gateEntered.TrySetResult();
                await release.Task.WaitAsync(token);
            };
            var first = provider.Preview(entry, firstToken.Token);
            await gateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var queuedToken = new CancellationTokenSource();
            var cancelled = provider.Preview(entry, queuedToken.Token);
            queuedToken.Cancel();
            await Assert.ThrowsAsync<TaskCanceledException>(async () => await cancelled);

            var queuedForEnd = provider.Preview(entry, CancellationToken.None);
            ((IProvider)provider).End();
            Assert.That((await queuedForEnd).State, Is.EqualTo(Brows.MBox.Format.MBoxBodyState.Unavailable));
            firstToken.Cancel();
            await Assert.ThrowsAsync<TaskCanceledException>(async () => await first);
        }
        finally {
            release.TrySetResult();
            provider.Dispose();
        }
    }

    [Test]
    public async Task Preview_QueuedDecodeReturnsUnavailableAfterRefreshRetiresItsSession() {
        var config = new MBoxConfig {
            MaxActivePreviewDecodes = 1,
        };
        var provider = await MBoxTestProvider.Create(_Path, config);
        var gateEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try {
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            var entry = provider.Entries[0];
            var generation = provider.ActiveGenerationId;
            provider.DecodeGateForTesting = async token => {
                gateEntered.TrySetResult();
                await release.Task.WaitAsync(token);
            };
            var first = provider.Preview(entry, CancellationToken.None);
            await gateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var queued = provider.Preview(entry, CancellationToken.None);

            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            release.TrySetResult();
            var results = await Task.WhenAll(first, queued);

            using (Assert.EnterMultipleScope()) {
                Assert.That(provider.ActiveGenerationId, Is.Not.EqualTo(generation));
                Assert.That(results.All(result => result.State == Brows.MBox.Format.MBoxBodyState.Unavailable), Is.True);
            }
        }
        finally {
            release.TrySetResult();
            provider.Dispose();
        }
    }

    [Test]
    public async Task Preview_ReturnsNothingWhenTheMessageIsOfARetiredGeneration() {
        var provider = await Provider();
        try {
            var entries = await Read(provider);
            await MBoxTestProvider.Refresh(provider, CancellationToken.None);
            var preview = await provider.Preview(entries[0], CancellationToken.None);
            Assert.That(preview.Text, Is.Null);
        }
        finally {
            provider.Dispose();
        }
    }

    [Test]
    public async Task Dispose_EndsTheProviderAndReleasesTheMailbox() {
        var provider = await Provider();
        await Read(provider);
        provider.Dispose();
        using (Assert.EnterMultipleScope()) {
            Assert.That(((IEntryProvider)provider).Observation.Observed, Is.Empty);
            Assert.That(() => {
                using var stream = new FileStream(_Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }, Throws.Nothing);
        }
    }

    [Test]
    public async Task End_ThenDisposeAndDisposeTwice_ReleasesTheMailboxOnce() {
        var provider = await Provider();
        await Read(provider);
        ((IProvider)provider).End();
        provider.Dispose();
        provider.Dispose();
        using var stream = new FileStream(_Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.That(stream.CanRead, Is.True);
    }

    [Test]
    public async Task Dispose_WhenCancellationCallbackThrows_StillEndsAndDisposesTheProvider() {
        var provider = await Provider();
        await Read(provider);
        using var registration = provider.Token.Register(() => {
            throw new InvalidOperationException("Injected cancellation callback failure.");
        });

        Assert.Throws<AggregateException>(() => provider.Dispose());

        using (Assert.EnterMultipleScope()) {
            Assert.That(((IEntryProvider)provider).Observation.Observed, Is.Empty);
            Assert.That(() => {
                using var stream = new FileStream(_Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }, Throws.Nothing);
        }
    }

    [Test]
    public async Task Dispose_DuringALeasedPreview_DiscardsResultAndClosesMailboxAfterReadCompletes() {
        var provider = await Provider();
        await Read(provider);
        var gateEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.DecodeGateForTesting = async token => {
            gateEntered.TrySetResult();
            await release.Task.WaitAsync(token);
        };
        try {
            var preview = provider.Preview(provider.Entries[0], CancellationToken.None);
            await gateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            provider.Dispose();
            Assert.That(() => {
                using var stream = new FileStream(_Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }, Throws.TypeOf<IOException>());

            release.TrySetResult();
            var result = await preview;
            Assert.That(result.State, Is.EqualTo(Brows.MBox.Format.MBoxBodyState.Unavailable));
            Assert.That(result.Text, Is.Null);
            Assert.That(() => {
                using var stream = new FileStream(_Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }, Throws.Nothing);
        }
        finally {
            release.TrySetResult();
            provider.Dispose();
        }
    }

    [Test]
    public async Task Dispose_WhenObservationClearThrows_StillEndsAndDisposesTheProvider() {
        var provider = await Provider();
        await Read(provider);
        var observation = (EntryObservation)((IEntryProvider)provider).Observation;
        var collection = (EntryObservationSource)observation.Source;
        provider.Entries[0].Select = true;
        EventHandler changed = (_, _) => {
            throw new InvalidOperationException("Injected collection clear failure.");
        };
        collection.SelectionChanged += changed;

        Assert.Throws<InvalidOperationException>(() => provider.Dispose());

        collection.SelectionChanged -= changed;
        using (Assert.EnterMultipleScope()) {
            Assert.That(observation.Observed, Is.Empty);
            Assert.That(collection.Selection, Is.Empty);
            Assert.That(() => {
                using var stream = new FileStream(_Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }, Throws.Nothing);
        }
    }
}
