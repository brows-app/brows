using Brows.Conf;
using Brows.Localization;
using Brows.Operations;
using Brows.Triggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

[TestFixture]
internal sealed class UpdateTest {
    private const string Running = "0.103.0.60";
    private const string Newer = "0.104.0.61";
    private const string Beta = "0.105.0-beta.62";

    private static AppUpdate Release(string version = Newer) {
        return new AppUpdate {
            Version = version,
            Page = new($"https://github.com/owner/name/releases/tag/v{version}"),
            Architecture = "x64",
            Package = new AppUpdatePackage {
                Name = $"brows-x64-{version}.msi",
                Url = new($"https://github.com/owner/name/releases/download/v{version}/brows-x64-{version}.msi"),
                Sha256 = new string('a', 64),
                Size = 1
            }
        };
    }

    [TestCase(false, null, null, null, false, null, null)]
    [TestCase(true, null, null, null, true, null, null)]
    [TestCase(true, false, null, null, false, null, null)]
    [TestCase(false, true, null, null, true, null, null)]
    [TestCase(false, null, " " + Beta, " ARM64 ", false, Beta, "ARM64")]
    [TestCase(false, null, " ", "", false, null, null)]
    public void Request_CombinesConfigurationAndOverrides(bool configured, bool? preRelease, string version,
        string arch, bool expectedPreRelease, string expectedVersion, string expectedArch) {
        var request = Update.Request(new() { Prerelease = configured }, new() {
            Prerelease = preRelease,
            Version = version,
            Architecture = arch
        });

        Assert.That(request, Is.EqualTo(new AppRequest {
            AllowPreRelease = expectedPreRelease,
            Architecture = expectedArch,
            Version = expectedVersion
        }));
    }

    [Test]
    public async Task Work_CurrentVersionReportsProgressAndStaysOpen() {
        using (var scenario = new Scenario()) {
            Assert.That(await scenario.Work(), Is.True);
            Assert.That(scenario.Progress.Invocations.Any(call =>
                Equals(call.Arguments[7], $"Brows {Running} is up to date")), Is.True);
            scenario.Installer.VerifyNoOtherCalls();
            Assert.That(scenario.Ends, Is.Zero);
        }
    }

    [TestCase("missing pin", "Update_NotFound")]
    [TestCase("portable", "Update_NotInstallable")]
    [TestCase("missing package", "Update_NoInstaller")]
    [TestCase("busy", "Update_InProgress")]
    public void Work_ReportsWhyUpdateCannotProceed(string reason, string message) {
        using (var scenario = new Scenario()) {
            var update = reason switch {
                "missing pin" => null,
                "missing package" => Release() with { Package = null },
                _ => Release()
            };
            scenario.Feed.Setup(feed => feed.Get(It.IsAny<AppRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(update);
            scenario.Installable.Setup(copy => copy.Get(It.IsAny<CancellationToken>())).ReturnsAsync(reason != "portable");

            var parameter = reason == "missing pin" ? Newer : null;
            Assert.That(async () => await scenario.Work(parameter),
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo(message));
            scenario.Installer.Verify(installer =>
                installer.Work(It.IsAny<AppUpdate>(), It.IsAny<IOperationProgress>(), It.IsAny<CancellationToken>()),
                Times.Exactly(reason == "busy" ? 1 : 0));
            Assert.That(scenario.Ends, Is.Zero);
        }
    }

    [Test]
    public async Task Work_ParsesPinArchitectureAndPrereleaseAndEndsAfterSuccessfulHandoff() {
        using (var scenario = new Scenario()) {
            var offered = Release(Beta);
            scenario.Config.Prerelease = true;
            var request = new AppRequest { Version = Beta, Architecture = "ARM64", AllowPreRelease = false };
            scenario.Feed.Setup(service => service.Get(request, It.IsAny<CancellationToken>())).ReturnsAsync(offered);
            scenario.Installer.Setup(installer =>
                    installer.Work(offered, scenario.Progress.Object, It.IsAny<CancellationToken>()))
                .Callback(() => Assert.That(scenario.Ends, Is.Zero)).ReturnsAsync(true);

            Assert.That(await scenario.Work($"{Beta} arch=ARM64 prerelease=false"), Is.True);
            scenario.Feed.Verify(service => service.Get(request, It.IsAny<CancellationToken>()), Times.Once);
            Assert.That(scenario.Ends, Is.EqualTo(1));
        }
    }

    [Test]
    public void Work_FeedArgumentErrorPropagatesWithoutEnding() {
        using (var scenario = new Scenario()) {
            scenario.Feed.Setup(feed => feed.Get(It.IsAny<AppRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("An unsupported installer architecture."));
            Assert.That(async () => await scenario.Work("arch=x86"),
                Throws.TypeOf<ArgumentException>().With.Message.EqualTo("An unsupported installer architecture."));
            scenario.Installer.VerifyNoOtherCalls();
            Assert.That(scenario.Ends, Is.Zero);
        }
    }

    [Test]
    public async Task Suggest_BackgroundNoticeShowsPinnedVersionOnlyOnEmptyPaletteWithMatchingSettings() {
        using (var scenario = new Scenario()) {
            scenario.Feed.Setup(feed => feed.Get(new AppRequest(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Release());
            await scenario.Init();
            await scenario.SecondLoad.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var notice = (await scenario.UpdateRows()).Single();
            using (Assert.EnterMultipleScope()) {
                Assert.That(notice.Input, Is.EqualTo($"brows-update {Newer}"));
                Assert.That(notice.GroupOrder, Is.EqualTo(-1));
                Assert.That(await scenario.UpdateRows(nonempty: true), Is.Empty);
            }
            scenario.Config.Prerelease = true;
            Assert.That(await scenario.UpdateRows(), Is.Empty);
        }
    }

    [TestCase("disabled")]
    [TestCase("portable")]
    [TestCase("no package")]
    [TestCase("error")]
    public async Task Init_BackgroundSkipsOrSuppressesUnavailableNotices(string reason) {
        using (var scenario = new Scenario()) {
            scenario.Config.Check = reason != "disabled";
            scenario.Installable.Setup(copy => copy.Get(It.IsAny<CancellationToken>())).ReturnsAsync(reason != "portable");
            var feed = scenario.Feed.Setup(service => service.Get(It.IsAny<AppRequest>(), It.IsAny<CancellationToken>()));
            if (reason == "error") {
                feed.ThrowsAsync(new InvalidOperationException("Test failure."));
            }
            else {
                feed.ReturnsAsync(Release() with { Package = null });
            }

            await scenario.Init();
            await scenario.SecondLoad.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(await scenario.UpdateRows(), Is.Empty);
            if (reason is "disabled" or "portable") {
                scenario.Feed.Verify(service => service.Get(It.IsAny<AppRequest>(), It.IsAny<CancellationToken>()),
                    Times.Never);
            }
        }
    }

    [Test]
    public async Task Init_CompletesWhileFeedIsStillRunning() {
        using (var scenario = new Scenario()) {
            var checking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            scenario.Feed.Setup(feed => feed.Get(It.IsAny<AppRequest>(), It.IsAny<CancellationToken>()))
                .Returns(async (AppRequest _, CancellationToken token) => {
                    checking.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return null;
                });

            await scenario.Init().WaitAsync(TimeSpan.FromSeconds(5));
            await checking.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(await scenario.UpdateRows(), Is.Empty);
        }
    }

    [Test]
    public async Task Suggest_OlderCompletionAndPinnedRunLeaveTheLatestDefaultNotice() {
        using (var scenario = new Scenario()) {
            var first = new TaskCompletionSource<AppUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
            scenario.Feed.SetupSequence(feed => feed.Get(new AppRequest(), It.IsAny<CancellationToken>()))
                .Returns(first.Task).ReturnsAsync(Release(Beta));
            scenario.Feed.Setup(feed => feed.Get(new AppRequest { Version = Newer }, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Release());

            var pending = scenario.Work();
            try {
                Assert.That(async () => await scenario.Work(),
                    Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("Update_InProgress"));
            }
            finally {
                first.SetResult(Release());
            }
            Assert.That(async () => await pending,
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("Update_InProgress"));
            Assert.That(async () => await scenario.Work(Newer),
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("Update_InProgress"));
            Assert.That((await scenario.UpdateRows()).Select(row => row.Input),
                Is.EqualTo(new[] { $"brows-update {Beta}" }));
        }
    }

    [TestCase(AppUpdateOutcomeKind.Failed, 1618, "Brows " + Newer + " failed (1618). See install.log.")]
    [TestCase(AppUpdateOutcomeKind.RestartRequired, 3010, "Restart for Brows " + Newer + " (3010). See install.log.")]
    [TestCase(AppUpdateOutcomeKind.NotStarted, 0, "Brows " + Newer + " did not close in time.")]
    [TestCase(AppUpdateOutcomeKind.Installed, 0, null)]
    public async Task Suggest_ShowsHandoffOutcomeRequiringAttention(AppUpdateOutcomeKind kind, int code,
                                                                     string description) {
        using (var scenario = new Scenario()) {
            scenario.Config.Check = false;
            scenario.Outcome.Setup(reader => reader.Get(It.IsAny<CancellationToken>())).ReturnsAsync(
                new AppUpdateOutcome { Version = Newer, Kind = kind, Log = "install.log", ExitCode = code });
            await scenario.Init();
            await scenario.SecondLoad.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var rows = await scenario.UpdateRows();
            if (description is null) {
                Assert.That(rows, Is.Empty);
                return;
            }
            var row = rows.Single();
            using (Assert.EnterMultipleScope()) {
                Assert.That(row.Description, Is.EqualTo(description));
                Assert.That(row.Input, Is.EqualTo("brows-update"));
            }
        }
    }

    private sealed class TestConfiguration : IConfiguration<UpdateConfig> {
        private readonly Action Loading;

        internal TestConfiguration(UpdateConfig value, Action loading) {
            Loaded = value;
            Loading = loading;
        }

        public UpdateConfig Loaded { get; }

        event EventHandler IConfiguration.Changed {
            add { }
            remove { }
        }

        Task<UpdateConfig> IConfiguration<UpdateConfig>.Load(CancellationToken token) {
            token.ThrowIfCancellationRequested();
            Loading();
            return Task.FromResult(Loaded);
        }
    }

    private sealed class Scenario : IDisposable {
        private readonly CancellationTokenSource Cancellation = new();

        private int ConfigLoads;

        private Update Command { get; }

        private static Mock<ICommandContext> Context(string parameter, bool nonempty = false) {
            var line = new Mock<ICommandLine>();
            line.Setup(value => value.HasParameter(out parameter)).Returns(parameter is not null);
            var text = nonempty ? "brows" : null;
            line.Setup(value => value.HasCommand(out text)).Returns(nonempty);
            var context = new Mock<ICommandContext>();
            var commandLine = line.Object;
            context.Setup(value => value.HasLine(out commandLine)).Returns(true);
            return context;
        }

        private static void SetTrigger(Update command) {
            var input = Mock.Of<IInputTrigger>(value => value.Input == "brows-update");
            var inputs = new Mock<IInputTriggerCollection>();
            inputs.SetupGet(value => value.Main).Returns(input);
            inputs.Setup(value => value.GetEnumerator()).Returns(() => Enumerable.Empty<IInputTrigger>().GetEnumerator());
            var trigger = Mock.Of<ICommandTrigger>(value => value.Inputs == inputs.Object);
            typeof(Command).GetProperty(nameof(Command.Trigger)).SetValue(command, trigger);
        }

        internal Mock<IAppUpdateAvailable> Feed { get; } = new();
        internal Mock<IAppUpdateInstallable> Installable { get; } = new();
        internal Mock<IAppUpdateInstaller> Installer { get; } = new();
        internal Mock<IAppUpdateOutcome> Outcome { get; } = new();
        internal Mock<IOperationProgress> Progress { get; } = new();
        internal UpdateConfig Config { get; } = new();
        internal TaskCompletionSource SecondLoad { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Ends { get; private set; }

        internal Scenario() {
            var configuration = new TestConfiguration(Config, () => {
                if (Interlocked.Increment(ref ConfigLoads) == 2) {
                    SecondLoad.SetResult();
                }
            });
            var configFile = Mock.Of<IConfigurationFile>(value => value.File<UpdateConfig>() == configuration);
            var translation = new Mock<ITranslationExport>();
            translation.Setup(value => value.Value("Update_Current")).Returns("Brows {0} is up to date");
            translation.Setup(value => value.Value("Update_Failed")).Returns("Brows {0} failed ({1}). See {2}.");
            translation.Setup(value => value.Value("Update_RestartRequired"))
                .Returns("Restart for Brows {0} ({1}). See {2}.");
            translation.Setup(value => value.Value("Update_NotStarted")).Returns("Brows {0} did not close in time.");
            Installable.Setup(value => value.Get(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Feed.Setup(value => value.Get(It.IsAny<AppRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AppUpdate)null);
            Outcome.Setup(value => value.Get(It.IsAny<CancellationToken>())).ReturnsAsync((AppUpdateOutcome)null);
            Installer.Setup(value => value.Work(
                It.IsAny<AppUpdate>(), It.IsAny<IOperationProgress>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var version = new Mock<IAppRunningVersion>();
            version.Setup(value => value.Get(It.IsAny<CancellationToken>())).ReturnsAsync(Running);

            Command = new Update {
                ConfigFile = configFile,
                RunningVersion = version.Object,
                UpdateAvailable = Feed.Object,
                UpdateInstallable = Installable.Object,
                UpdateInstaller = Installer.Object,
                UpdateOutcome = Outcome.Object,
                Translation = translation.Object,
                Ending = _ => () => Ends++,
                RemindDelay = TimeSpan.Zero,
                RemindInterval = TimeSpan.FromMilliseconds(20)
            };

            var cli = typeof(Command).GetProperty("CommandCliProvider", BindingFlags.Instance | BindingFlags.NonPublic);
            cli.SetValue(Command, Activator.CreateInstance(cli.PropertyType, nonPublic: true));
            SetTrigger(Command);
        }

        internal Task<bool> Work(string parameter = null) {
            var context = Context(parameter);
            Func<IOperationProgress, CancellationToken, Task<bool>> operation = null;
            context.Setup(value => value.Operate(It.IsAny<Func<IOperationProgress, CancellationToken, Task<bool>>>()))
                .Callback<Func<IOperationProgress, CancellationToken, Task<bool>>>(work => operation = work)
                .Returns(true);
            Assert.That(((ICommand)Command).TriggeredWork(context.Object), Is.True);
            Assert.That(operation, Is.Not.Null);
            return operation(Progress.Object, Cancellation.Token);
        }

        internal async Task Init() {
            await ((ICommand)Command).Init(Cancellation.Token);
            SetTrigger(Command);
        }

        internal async Task<List<ICommandSuggestion>> UpdateRows(bool nonempty = false) {
            var rows = new List<ICommandSuggestion>();
            await foreach (var row in ((ICommand)Command).Suggest(Context(null, nonempty).Object, Cancellation.Token)) {
                if (row.GroupOrder == -1) {
                    rows.Add(row);
                }
            }
            return rows;
        }

        void IDisposable.Dispose() {
            Cancellation.Cancel();
            Cancellation.Dispose();
        }
    }
}
