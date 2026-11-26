using System.Runtime.InteropServices;

namespace Brows;

[TestFixture]
internal sealed class AppArchitectureTest {
    [TestCase(Architecture.X64, "x64")]
    [TestCase(Architecture.Arm64, "arm64")]
    [TestCase(Architecture.X86, null)]
    [TestCase(Architecture.Arm, null)]
    public void From_MapsProcessArchitecture(Architecture input, string expected) {
        Assert.That(AppArchitecture.From(input), Is.EqualTo(expected));
    }

    [TestCase("x64", "x64")]
    [TestCase("ARM64", "arm64")]
    [TestCase(" arm64 ", "arm64")]
    [TestCase("x86", null)]
    [TestCase("amd64", null)]
    [TestCase("", null)]
    [TestCase(null, null)]
    public void TryNormalize_RecognizesSupportedTokens(string input, string expected) {
        var supported = AppArchitecture.TryNormalize(input, out var actual);
        using (Assert.EnterMultipleScope()) {
            Assert.That(supported, Is.EqualTo(expected is not null));
            Assert.That(actual, Is.EqualTo(expected));
        }
    }

    [TestCase("arm64", Architecture.X64, false)]
    [TestCase("x64", Architecture.X64, true)]
    [TestCase("arm64", Architecture.Arm64, true)]
    [TestCase("x64", Architecture.Arm64, true)]
    [TestCase("x64", Architecture.X86, false)]
    public void Runs_RespectsHostArchitecture(string token, Architecture os, bool expected) {
        Assert.That(AppArchitecture.Runs(token, os), Is.EqualTo(expected));
    }
}
