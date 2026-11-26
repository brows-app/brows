using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Updates;

[TestFixture]
[NonParallelizable]
internal sealed class UpdaterHandoffTest {
    private static void AssertOutcome(HandoffTestDirectory directory, string kind, int code) {
        using (var doc = JsonDocument.Parse(File.ReadAllText(directory.Outcome))) {
            var outcome = doc.RootElement;
            using (Assert.EnterMultipleScope()) {
                Assert.That(outcome.GetProperty("version").GetString(), Is.EqualTo(directory.Version));
                Assert.That(outcome.GetProperty("kind").GetString(), Is.EqualTo(kind));
                Assert.That(outcome.GetProperty("exitCode").GetInt32(), Is.EqualTo(code));
                Assert.That(outcome.GetProperty("log").GetString(), Is.EqualTo(directory.Log));
                Assert.That(File.Exists(directory.Outcome + ".tmp"), Is.False);
            }
        }
    }

    [TestCase(0, "Installed", true)]
    [TestCase(3010, "RestartRequired", true)]
    [TestCase(1603, "Failed", false)]
    [TestCase(1618, "Failed", false)]
    public void Run_RecordsInstallerResultCleansOlderSourcesOnSuccessAndRelaunches(int code, string kind,
                                                                                 bool cleaned) {
        using (var directory = new HandoffTestDirectory()) {
            File.WriteAllText(directory.Launch, "");
            File.WriteAllText(directory.Msi, "");
            var older = Directory.CreateDirectory(Path.Combine(directory.Updates, "0.103.0-beta.61")).FullName;
            var newer = Directory.CreateDirectory(Path.Combine(directory.Updates, "0.104.0.63")).FullName;
            var unrelated = Directory.CreateDirectory(Path.Combine(directory.Updates, "handoff")).FullName;

            ProcessStartInfo launch = null;
            var handoff = new UpdaterHandoff(directory.Parse()) {
                Wait = (_, _) => true,
                RunInstaller = _ => code,
                LaunchBrows = info => {
                    Assert.That(File.Exists(directory.Outcome), Is.True);
                    launch = info;
                }
            };

            Assert.That(handoff.Run(), Is.EqualTo(code));
            AssertOutcome(directory, kind, code);
            Assert.That(launch, Is.Not.Null);
            using (Assert.EnterMultipleScope()) {
                Assert.That(Directory.Exists(older), Is.EqualTo(!cleaned));
                Assert.That(Directory.Exists(newer), Is.True);
                Assert.That(Directory.Exists(unrelated), Is.True);
                Assert.That(File.Exists(directory.Msi), Is.True);
                Assert.That(launch.FileName, Is.EqualTo(directory.Launch));
                Assert.That(launch.UseShellExecute, Is.True);
            }
        }
    }

    [Test]
    public async Task Run_WaitsForBrowsBeforeStartingInstaller() {
        using (var directory = new HandoffTestDirectory())
        using (var exited = new ManualResetEventSlim()) {
            var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var installed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var handoff = new UpdaterHandoff(directory.Parse()) {
                Wait = (_, _) => {
                    waiting.SetResult();
                    return exited.Wait(TimeSpan.FromSeconds(5));
                },
                RunInstaller = _ => {
                    installed.SetResult();
                    return 0;
                }
            };

            var run = Task.Run(handoff.Run);
            try {
                await waiting.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.That(installed.Task.IsCompleted, Is.False);
            }
            finally {
                exited.Set();
                await run.WaitAsync(TimeSpan.FromSeconds(5));
            }
            Assert.That(run.Result, Is.Zero);
            Assert.That(installed.Task.IsCompleted, Is.True);
        }
    }

    [Test]
    public void Run_BrowsExitTimeoutRecordsNotStartedWithoutInstallingOrLaunching() {
        using (var directory = new HandoffTestDirectory()) {
            File.WriteAllText(directory.Launch, "");
            var timeout = TimeSpan.FromMilliseconds(10);
            var launched = false;
            var handoff = new UpdaterHandoff(directory.Parse()) {
                ExitTimeout = timeout,
                Wait = (pid, limit) => {
                    Assert.That(pid, Is.EqualTo(123));
                    Assert.That(limit, Is.EqualTo(timeout));
                    return false;
                },
                RunInstaller = _ => throw new AssertionException("Unexpected installer start."),
                LaunchBrows = _ => launched = true
            };

            Assert.That(handoff.Run(), Is.EqualTo(1));
            AssertOutcome(directory, "NotStarted", 0);
            Assert.That(launched, Is.False);
        }
    }

    [Test]
    public void Run_ExistingMutexExitsWithoutWaitingInstallingOrLaunching() {
        var launched = false;
        using (var directory = new HandoffTestDirectory())
        using (var mutex = new Mutex(initiallyOwned: false, UpdaterHandoff.MutexName)) {
            var handoff = new UpdaterHandoff(directory.Parse()) {
                Wait = (_, _) => throw new AssertionException("Unexpected process wait."),
                RunInstaller = _ => throw new AssertionException("Unexpected installer start."),
                LaunchBrows = _ => launched = true
            };

            Assert.That(handoff.Run(), Is.EqualTo(3));
            Assert.That(File.Exists(directory.Outcome), Is.False);
            Assert.That(launched, Is.False);
        }
    }

    [Test]
    public void Run_MissingLaunchPathStillRecordsOutcome() {
        var launched = false;
        using (var directory = new HandoffTestDirectory()) {
            var handoff = new UpdaterHandoff(directory.Parse()) {
                Wait = (_, _) => true,
                RunInstaller = _ => 1603,
                LaunchBrows = _ => launched = true
            };

            Assert.That(handoff.Run(), Is.EqualTo(1603));
            AssertOutcome(directory, "Failed", 1603);
            Assert.That(launched, Is.False);
        }
    }

    [Test]
    public void Run_InstallerStartExceptionRecordsFailureAndRelaunches() {
        using (var directory = new HandoffTestDirectory()) {
            File.WriteAllText(directory.Launch, "");
            var launched = false;
            var handoff = new UpdaterHandoff(directory.Parse()) {
                Wait = (_, _) => true,
                RunInstaller = _ => throw new IOException("Test installer start failure."),
                LaunchBrows = _ => launched = true
            };

            Assert.That(handoff.Run(), Is.EqualTo(1));
            AssertOutcome(directory, "Failed", -1);
            Assert.That(launched, Is.True);
        }
    }
}
