using Domore.Conf.Cli;
using Domore.Conf.Extensions;

namespace Brows.Commands;

[TestFixture]
internal sealed class TarParameterTest {
    [Test]
    public void Configure_PreservesConfigurationAndCommandLineBinding() {
        var parameter = new TarParameter().ConfFrom(
            "force=Extract\noverwrite=true",
            key: "");
        var cli = new CliProvider(new CliSetup());
        parameter = cli.Configure(parameter, "archive.tar force=Compress");

        using (Assert.EnterMultipleScope()) {
            Assert.That(parameter.Output, Is.EqualTo("archive.tar"));
            Assert.That(parameter.Direction, Is.EqualTo(TarDirection.Compress));
            Assert.That(parameter.Overwrite, Is.True);
        }
    }
}
