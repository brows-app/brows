using Brows.Support;
using Domore.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

[TestFixture]
internal sealed class FindInInfoTest {
    private static int PoolCount(DecodedTextOptions options) {
        var property = typeof(DecodedTextOptions).GetProperty(
            "PoolCount", BindingFlags.Instance | BindingFlags.NonPublic);
        return (int)property.GetValue(options);
    }

    [Test]
    public async Task Process_ReturnsEveryFileDecoderBufferAndLeavesConfigurationUnchanged() {
        using var fixture = new TempDirectory();
        var parameter = new Find.Parameter { Pattern = "needle", In = FindIn.FileText };
        var settings = new FindSettings(parameter);
        var created = new List<DecodedTextOptions>();
        DecodedTextOptions Decoder() {
            var options = settings.CreateDecoder();
            created.Add(options);
            return options;
        }
        var finder = new FindInInfo(fixture.Root, settings, Decoder);

        for (var i = 0; i < 20; i++) {
            var file = fixture.CreateFile($"{i}.txt", new string('x', 5000) + "\nneedle");
            var outcome = await finder.Process(file, default);
            Assert.That(outcome.Matched, Is.True);
            Assert.That(PoolCount(created[i]), Is.Zero, "Each file must release its own decoder resources.");
        }

        Assert.That(created, Is.Unique);
        Assert.That(PoolCount(parameter.Decoder), Is.Zero);
        Assert.That(created[0].Encoding, Is.Not.SameAs(created[1].Encoding));
        Assert.That(created[0].StreamBuffer, Is.Not.SameAs(created[1].StreamBuffer));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Process_ReturnsBuffersForInvalidEncodingAndReadErrors(bool locked) {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("invalid.txt");
        File.WriteAllBytes(file.FullName, [0xff, 0xff, 0xff]);
        using var stream = locked ? file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
        var settings = new FindSettings(new Find.Parameter { Pattern = "needle", In = FindIn.FileText });
        var options = settings.CreateDecoder();
        var finder = new FindInInfo(fixture.Root, settings, () => options);

        var outcome = await finder.Process(file, default);

        Assert.That(outcome.Matched, Is.False);
        Assert.That(outcome.Errors, Is.EqualTo(locked ? 1 : 0));
        Assert.That(PoolCount(options), Is.Zero);
    }

    [Test]
    public async Task Process_CancellationDuringDecoderSetupIsNotAFileError() {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("file.txt", "needle");
        using var cancellation = new CancellationTokenSource();
        var settings = new FindSettings(new Find.Parameter { Pattern = "needle", In = FindIn.FileText });
        var options = settings.CreateDecoder();
        var finder = new FindInInfo(fixture.Root, settings, () => {
            cancellation.Cancel();
            return options;
        });

        await Assert.ThrowsAsync<OperationCanceledException>(() => finder.Process(file, cancellation.Token));

        Assert.That(PoolCount(options), Is.Zero);
    }

    [Test]
    public async Task Process_SnapshotsDecoderConfigurationBeforeConcurrentWork() {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("file.txt", "needle");
        var parameter = new Find.Parameter { Pattern = "needle", In = FindIn.FileText };
        parameter.Decoder.Encoding.Add("utf-8");
        var settings = new FindSettings(parameter);
        parameter.Decoder.Encoding.Clear();
        parameter.Decoder.Encoding.Add("does-not-exist");
        parameter.Include.Add("*.no-match");
        parameter.Pattern = "absent";
        var finder = new FindInInfo(fixture.Root, settings);

        var outcome = await finder.Process(file, default);

        Assert.That(outcome.Matched, Is.True);
    }

    [Test]
    public async Task Process_ReportsRegexTimeoutDuringTextMatchAsFileError() {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("large.txt", new string('a', 8 * 1024 * 1024));
        var parameter = new Find.Parameter { Pattern = "*needle*", In = FindIn.FileText };
        var matcher = new FindMatcher(parameter.Pattern, ignoreCase: false, TimeSpan.FromTicks(1));
        var finder = new FindInInfo(fixture.Root, new FindSettings(parameter, matcher));

        var outcome = await finder.Process(file, default);

        Assert.That(outcome.Errors, Is.EqualTo(1));
        Assert.That(outcome.Matched, Is.False);
        Assert.That(outcome.Rows, Has.Length.EqualTo(1));
        Assert.That(outcome.Rows.Single(), Is.TypeOf<FoundInFileError>());
        Assert.That(((FoundInFileError)outcome.Rows.Single()).Exception, Is.TypeOf<RegexMatchTimeoutException>());
    }
}
