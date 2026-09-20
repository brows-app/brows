using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace Brows.CLI.Commands;

[TestFixture]
internal sealed class AboutCommandTest {
    [Test]
    public async Task Run_OutputIsNameAndVersion() {
        BrowsTest.Ignore(BrowsTestEnv.GitHubAction);
        var data = await CliProgram.Run("Brows.CLI.Sample.exe", "about");
        var actual = data.OutputLines;
        var expected = string.Join(Environment.NewLine, [
            "Brows.CLI.Sample",
            "99.98.97.96"]);
        Assert.That(data.Output, Does.StartWith(expected));
    }
}
