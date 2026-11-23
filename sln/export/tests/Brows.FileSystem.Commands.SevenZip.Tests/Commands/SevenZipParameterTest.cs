using Domore.Conf.Cli;
using Domore.Conf.Extensions;

namespace Brows.Commands;

[TestFixture]
internal sealed class SevenZipParameterTest {
    [Test]
    public void Configure_PreservesConfigurationAndCommandLineBinding() {
        var parameter = new SevenZip.Parameter().ConfFrom(
            "force=Extract\ncompression=2\noverwrite=true",
            key: "");
        var cli = new CliProvider(new CliSetup());
        parameter = cli.Configure(parameter, "archive.7z force=Compress compression=7");

        using (Assert.EnterMultipleScope()) {
            Assert.That(parameter.Output, Is.EqualTo("archive.7z"));
            Assert.That(parameter.Direction, Is.EqualTo(SevenZipDirection.Compress));
            Assert.That(parameter.CompressionLevel, Is.EqualTo(7));
            Assert.That(parameter.Overwrite, Is.True);
        }
    }
}
