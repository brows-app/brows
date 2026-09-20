using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition.Exports;

[TestFixture]
internal sealed class ExportWithPeriodicTaskTest {
    private IImportSandbox Sandbox;

    private sealed class Fetcher : ExportWithPeriodicTask {
        protected override TimeSpan Period => TimeSpan.FromMilliseconds(1);

        protected override Task PeriodicTask(CancellationToken token) {
            FetchCount++;
            return Task.CompletedTask;
        }

        public int FetchCount { get; private set; }
    }

    [SetUp]
    public void SetUp() {
        Sandbox = ImportSandbox.Create(inject: () => [new Fetcher()]);
    }

    [TearDown]
    public void TearDown() {
        Sandbox.Dispose();
    }

    [Test]
    public async Task PeriodicTask_IsCalled() {
        var fetchCount = 0;
        await Sandbox.Enter(async sandbox => {
            await Task.Delay(50);
            var subject = sandbox.Imports.Find<Fetcher>();
            fetchCount = subject.FetchCount;
        });
        Assert.That(fetchCount, Is.GreaterThan(0));
    }

    [Test]
    public async Task PeriodicTask_IsCalledRepeatedly() {
        var fetchCount1 = 0;
        var fetchCount2 = 0;
        await Sandbox.Enter(async sandbox => {
            var subject = sandbox.Imports.Find<Fetcher>();
            await Task.Delay(50);
            fetchCount1 = subject.FetchCount;
            await Task.Delay(50);
            fetchCount2 = subject.FetchCount;
        });
        Assert.That(fetchCount2, Is.GreaterThan(fetchCount1));
    }
}
