using NUnit.Framework;
using System.Collections.Generic;

namespace Brows.CLI;

[TestFixture]
internal sealed class CliProgramDataTest {
    [Test]
    public void Constructor_SetsProperties() {
        var output = "output text";
        var log = "log text";
        var data = new CliProgramData(false, output, log);
        using (Assert.EnterMultipleScope()) {
            Assert.That(data.Canceled, Is.False);
            Assert.That(data.Output, Is.EqualTo(output));
            Assert.That(data.Log, Is.EqualTo(log));
        }
    }

    [Test]
    public void Constructor_SetsCanceled() {
        var data = new CliProgramData(true, "", "");
        Assert.That(data.Canceled, Is.True);
    }

    [Test]
    public void OutputLines_ReturnsTrimmedLines() {
        var output = " line1 \nline2\n  line3  ";
        var data = new CliProgramData(false, output, "");
        var expected = new List<string> { "line1", "line2", "line3" };

        Assert.That(data.OutputLines, Is.EqualTo(expected));
    }

    [Test]
    public void OutputLines_EmptyOrWhitespace_ReturnsEmptyList() {
        var data1 = new CliProgramData(false, "", "");
        var data2 = new CliProgramData(false, "   \n   ", "");
        using (Assert.EnterMultipleScope()) {
            Assert.That(data1.OutputLines, Is.Empty);
            Assert.That(data2.OutputLines, Is.Empty);
        }
    }

    [Test]
    public void LogLines_ReturnsTrimmedLines() {
        var log = " log1 \nlog2\n  log3  ";
        var data = new CliProgramData(false, "", log);
        var expected = new List<string> { "log1", "log2", "log3" };

        Assert.That(data.LogLines, Is.EqualTo(expected));
    }

    [Test]
    public void LogLines_EmptyOrWhitespace_ReturnsEmptyList() {
        var data1 = new CliProgramData(false, "", "");
        var data2 = new CliProgramData(false, "", "   \n   ");
        using (Assert.EnterMultipleScope()) {
            Assert.That(data1.LogLines, Is.Empty);
            Assert.That(data2.LogLines, Is.Empty);
        }
    }

    [Test]
    public void OutputLines_CalledMultipleTimes_ReturnsSameInstance() {
        var data = new CliProgramData(false, "a\nb", "");
        var first = data.OutputLines;
        var second = data.OutputLines;
        Assert.That(ReferenceEquals(first, second), Is.True);
    }

    [Test]
    public void LogLines_CalledMultipleTimes_ReturnsSameInstance() {
        var data = new CliProgramData(false, "", "x\ny");
        var first = data.LogLines;
        var second = data.LogLines;
        Assert.That(ReferenceEquals(first, second), Is.True);
    }
}