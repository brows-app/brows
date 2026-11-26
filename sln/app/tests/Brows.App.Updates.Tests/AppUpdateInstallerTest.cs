using Brows.Composition;
using Brows.Operations;
using Brows.Updates;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
[NonParallelizable]
internal sealed class AppUpdateInstallerTest {
    private const string Repository = "owner/name";
    private const string HelperFile = "brows-update.exe";

    private static AppUpdate Invalid(AppUpdate update, string field) {
        var package = update.Package;
        return field switch {
            "null" => null,
            "package" => update with { Package = null },
            "relative version" => update with { Version = @"..\x" },
            "absolute version" => update with { Version = @"C:\x" },
            "older version" => update with { Version = "0.103.0.59" },
            "equal version" => update with { Version = "0.103.0.60" },
            "version metadata" => update with { Version = "0.104.0.61+abc" },
            "unknown architecture" => update with { Architecture = "amd64" },
            "non-normalized architecture" => update with { Architecture = "X64" },
            "incompatible architecture" => update with { Architecture = "arm64" },
            "wrong name" => update with { Package = package with { Name = "other.msi" } },
            "foreign url" => update with { Package = package with { Url = new("https://example.com/file.msi") } },
            "insecure url" => update with {
                Package = package with {
                    Url = new(
                package.Url.AbsoluteUri.Replace("https:", "http:", StringComparison.Ordinal))
                }
            },
            "digest" => update with { Package = package with { Sha256 = new string('g', 64) } },
            "empty" => update with { Package = package with { Size = 0 } },
            "oversized" => update with { Package = package with { Size = (1L << 30) + 1 } },
            _ => throw new ArgumentException(nameof(field))
        };
    }

    private static void WriteHelper(UpdateInstallation installation) {
        Directory.CreateDirectory(installation.ProgramDirectory);
        File.WriteAllText(Path.Combine(installation.ProgramDirectory, HelperFile), HelperFile);
    }

    private static Task<bool> Work(IAppUpdateInstaller installer, AppUpdate update) {
        return installer.Work(update, Mock.Of<IOperationProgress>(), CancellationToken.None);
    }

    [Test]
    public void Work_PortableCopyCannotDownloadOrStartHelper() {
        using (var directory = new UpdateTestDirectory())
        using (var handler = new UpdateHttpHandler(_ => throw new AssertionException("Unexpected HTTP request.")))
        using (var http = new HttpClient(handler)) {
            var installation = directory.Installation(installable: false);
            var installer = new AppUpdateInstaller {
                Installation = installation,
                Repository = Repository,
                Http = http,
                StartHelper = _ => throw new AssertionException("Unexpected helper start.")
            };

            Assert.That(async () => await Work(installer, directory.Update()),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(Directory.Exists(installation.UpdateDirectory), Is.False);
        }
    }

    [TestCase("null")]
    [TestCase("package")]
    [TestCase("relative version")]
    [TestCase("absolute version")]
    [TestCase("older version")]
    [TestCase("equal version")]
    [TestCase("version metadata")]
    [TestCase("unknown architecture")]
    [TestCase("non-normalized architecture")]
    [TestCase("incompatible architecture")]
    [TestCase("wrong name")]
    [TestCase("foreign url")]
    [TestCase("insecure url")]
    [TestCase("digest")]
    [TestCase("empty")]
    [TestCase("oversized")]
    public void Work_RejectsInvalidDescriptorBeforeDiskAndNetwork(string field) {
        using (var directory = new UpdateTestDirectory())
        using (var handler = new UpdateHttpHandler(_ => throw new AssertionException("Unexpected HTTP request.")))
        using (var http = new HttpClient(handler)) {
            var installation = directory.Installation();
            var installer = new AppUpdateInstaller {
                Installation = installation,
                Repository = Repository,
                Http = http,
                StartHelper = _ => throw new AssertionException("Unexpected helper start.")
            };

            var invalid = Invalid(directory.Update(), field);
            Assert.That(async () => await Work(installer, invalid), Throws.InstanceOf<ArgumentException>());
            Assert.That(Directory.Exists(installation.UpdateDirectory), Is.False);
        }
    }

    [Test]
    public async Task Work_StartsCopiedHelperWithVerifiedInstallerAndExactArguments() {
        using (var directory = new UpdateTestDirectory())
        using (var http = new HttpClient(new UpdateHttpHandler(_ =>
                   new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(directory.Bytes) }))) {
            var installation = directory.Installation();
            WriteHelper(installation);
            var update = directory.Update();
            var cache = Path.Combine(installation.UpdateDirectory, update.Version);
            var handoff = Path.Combine(installation.UpdateDirectory, "handoff");
            var msi = Path.Combine(cache, update.Package.Name);
            ProcessStartInfo started = null;
            var installer = new AppUpdateInstaller {
                Installation = installation,
                Repository = Repository,
                Http = http,
                StartHelper = info => {
                    Assert.That(File.ReadAllBytes(msi), Is.EqualTo(directory.Bytes));
                    started = info;
                    return true;
                }
            };

            Assert.That(await Work(installer, update), Is.True);
            Assert.That(started, Is.Not.Null);
            Assert.That(UpdaterArguments.TryParse(started.Arguments, out var arguments), Is.True);
            using (Assert.EnterMultipleScope()) {
                Assert.That(started.FileName, Is.EqualTo(Path.Combine(handoff, HelperFile)));
                Assert.That(started.UseShellExecute, Is.False);
                Assert.That(arguments.Pid, Is.EqualTo(Environment.ProcessId));
                Assert.That(arguments.Msi, Is.EqualTo(msi));
                Assert.That(arguments.Log, Is.EqualTo(Path.Combine(cache, "install.log")));
                Assert.That(arguments.Outcome, Is.EqualTo(Path.Combine(installation.UpdateDirectory, "outcome.json")));
                Assert.That(arguments.Version.Text, Is.EqualTo(update.Version));
                Assert.That(arguments.Updates, Is.EqualTo(installation.UpdateDirectory));
                Assert.That(arguments.Launch, Is.EqualTo(Path.Combine(installation.AppDirectory, "brows.exe")));
                Assert.That(Directory.GetFiles(handoff), Is.EqualTo(new[] { started.FileName }));
            }
        }
    }

    [Test]
    public async Task Work_ConcurrentDownloadReturnsFalseAndFailedStarterCanRetry() {
        var downloading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var directory = new UpdateTestDirectory())
        using (var http = new HttpClient(new UpdateHttpHandler(async (_, token) => {
            downloading.SetResult();
            await finish.Task.WaitAsync(token);
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(directory.Bytes) };
        }))) {
            var installation = directory.Installation();
            WriteHelper(installation);
            var attempts = 0;
            var installer = new AppUpdateInstaller {
                Installation = installation,
                Repository = Repository,
                Http = http,
                StartHelper = _ => ++attempts == 1 ? throw new InvalidOperationException("Test start failure.") : true
            };

            var update = directory.Update();
            var first = Work(installer, update);
            try {
                await downloading.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.That(await Work(installer, update), Is.False);
            }
            finally {
                finish.SetResult();
            }
            Assert.That(async () => await first, Throws.TypeOf<InvalidOperationException>());
            Assert.That(await Work(installer, update), Is.True);
            Assert.That(attempts, Is.EqualTo(2));
        }
    }

    [Test]
    public void Work_MissingHelperPreventsHandoff() {
        using (var directory = new UpdateTestDirectory())
        using (var http = new HttpClient(new UpdateHttpHandler(_ =>
                   new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(directory.Bytes) }))) {
            var installation = directory.Installation();
            Directory.CreateDirectory(installation.ProgramDirectory);
            var installer = new AppUpdateInstaller {
                Installation = installation,
                Repository = Repository,
                Http = http,
                StartHelper = _ => throw new AssertionException("Unexpected helper start.")
            };

            Assert.That(async () => await Work(installer, directory.Update()), Throws.TypeOf<FileNotFoundException>());
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Init_OnlyCleansAnInactiveHelper(bool active) {
        using (var directory = new UpdateTestDirectory())
        using (var mutex = active ? new Mutex(false, UpdaterHandoff.MutexName) : null)
        using (var http = new HttpClient(new UpdateHttpHandler(_ =>
                   throw new AssertionException("Unexpected HTTP request.")))) {
            var installation = directory.Installation();
            var handoff = Path.Combine(installation.UpdateDirectory, "handoff");
            Directory.CreateDirectory(handoff);
            var installer = new AppUpdateInstaller {
                Installation = installation,
                Repository = Repository,
                Http = http,
                StartHelper = _ => throw new AssertionException("Unexpected helper start.")
            };
            if (active) {
                Assert.That(await Work(installer, directory.Update()), Is.False);
            }
            await ((IExportAndInit)installer).Init(null, CancellationToken.None);

            Assert.That(Directory.Exists(handoff), Is.EqualTo(active));
        }
    }
}
