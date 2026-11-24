using Domore.Logs;
using System;
using System.Linq;

namespace Brows.Agent;

[TestFixture]
[NonParallelizable]
internal sealed class AgentLogTest {
    private static ILogEntry Entry(LogSeverity severity, params string[] list) {
        var entry = new Mock<ILogEntry>();
        entry.Setup(item => item.LogDate).Returns(new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc));
        entry.Setup(item => item.LogList).Returns(list);
        entry.Setup(item => item.LogName).Returns(nameof(AgentLogTest));
        entry.Setup(item => item.LogSeverity).Returns(severity);
        entry.Setup(item => item.LogType).Returns(typeof(AgentLogTest));
        return entry.Object;
    }

    [Test]
    public void Receive_NumbersEntriesInOrder() {
        using var log = new AgentLog();
        log.Receive(Entry(LogSeverity.Info, "first"));
        log.Receive(Entry(LogSeverity.Warn, "second", "line"));
        var entries = log.Since(0);
        Assert.That(entries.Select(entry => entry.Seq), Is.EqualTo(new long[] { 1, 2 }));
        using (Assert.EnterMultipleScope()) {
            Assert.That(entries[0].Text, Is.EqualTo("first"));
            Assert.That(entries[1].Text, Is.EqualTo("second / line"));
            Assert.That(entries[1].Severity, Is.EqualTo(LogSeverity.Warn));
            Assert.That(entries[1].Name, Is.EqualTo(nameof(AgentLogTest)));
        }
    }

    [Test]
    public void Receive_KeepsOnlyTheMostRecentEntries() {
        using var log = new AgentLog(capacity: 3);
        foreach (var text in new[] { "a", "b", "c", "d", "e" }) {
            log.Receive(Entry(LogSeverity.Info, text));
        }
        var entries = log.Since(0);
        Assert.That(entries.Select(entry => entry.Text), Is.EqualTo(new[] { "c", "d", "e" }));
        Assert.That(entries.Select(entry => entry.Seq), Is.EqualTo(new long[] { 3, 4, 5 }));
    }

    [Test]
    public void Since_ReturnsOnlyLaterEntries() {
        using var log = new AgentLog();
        log.Receive(Entry(LogSeverity.Info, "a"));
        log.Receive(Entry(LogSeverity.Info, "b"));
        log.Receive(Entry(LogSeverity.Info, "c"));
        var entries = log.Since(2);
        Assert.That(entries.Select(entry => entry.Text), Is.EqualTo(new[] { "c" }));
    }

    [Test]
    public void Errors_ReturnsOnlyErrorAndCriticalEntries() {
        using var log = new AgentLog();
        log.Receive(Entry(LogSeverity.Info, "info"));
        log.Receive(Entry(LogSeverity.Warn, "warn"));
        log.Receive(Entry(LogSeverity.Error, "error"));
        log.Receive(Entry(LogSeverity.Critical, "critical"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(log.Errors(0).Select(entry => entry.Text), Is.EqualTo(new[] { "error", "critical" }));
            Assert.That(log.Errors(3).Select(entry => entry.Text), Is.EqualTo(new[] { "critical" }));
        }
    }

    [Test]
    public void Receive_ConvertsTheEntryTimeToLocalTime() {
        using var log = new AgentLog();
        log.Receive(Entry(LogSeverity.Info, "a"));
        var expected = new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc).ToLocalTime();
        Assert.That(log.Since(0)[0].Time, Is.EqualTo(expected));
    }

    [Test]
    public void Format_IncludesSequenceSeverityNameAndText() {
        var entry = new AgentLogEntry(7, new DateTime(2026, 11, 23, 8, 9, 10, 11), LogSeverity.Error, "Name", "Text");
        Assert.That(entry.Format(), Is.EqualTo("7 08:09:10.011 Error Name: Text"));
    }

    [Test]
    public void Subscribe_ReceivesLogsUntilDisposed() {
        var log = new AgentLog();
        var source = Logging.For(typeof(AgentLogTest));
        var marker = Guid.NewGuid().ToString();
        log.Subscribe();
        try {
            source.Info(marker, "subscribed");
        }
        finally {
            log.Dispose();
        }
        source.Info(marker, "disposed");
        var texts = log.Since(0).Where(entry => entry.Text.Contains(marker)).Select(entry => entry.Text).ToList();
        Assert.That(texts, Has.Count.EqualTo(1));
        Assert.That(texts[0], Does.Contain("subscribed"));
    }

    [Test]
    public void Constructor_NonPositiveCapacity_Throws() {
        Assert.That(() => new AgentLog(capacity: 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }
}
