using Domore.Conf.Cli;

namespace Brows;

[TestFixture]
public sealed class FileProtocolRenameCommandLineTest {
    [Test]
    public void QuoteArgument_RoundTripsCliQuoteConcatenationAndLiteralBackslashes() {
        const string name = "both 'single' and \"double\" slash\\name";
        var argument = FileProtocolRenameCommandLine.QuoteArgument(name, name.Length, out var selectedLength);
        var cli = new CliProvider(new CliSetup());
        var cliParsed = cli.Configure(new Argument(), argument).Name;

        Assert.Multiple(() => {
            Assert.That(cliParsed, Is.EqualTo(name));
            Assert.That(FileProtocolRenameCommandLine.TryParseArgument(argument, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(name));
            Assert.That(selectedLength, Is.EqualTo(argument.Length - 2));
        });
    }

    [TestCase(" leading and trailing ")]
    [TestCase("   ")]
    public void QuoteArgument_PreservesBoundaryAndOnlySpaces(string name) {
        var argument = FileProtocolRenameCommandLine.QuoteArgument(name, name.Length, out var selectedLength);

        Assert.That(FileProtocolRenameCommandLine.TryParseArgument(argument, out var parsed), Is.True);

        Assert.Multiple(() => {
            Assert.That(parsed, Is.EqualTo(name));
            Assert.That(selectedLength, Is.EqualTo(name.Length));
        });
    }

    [TestCase("report.tar.gz", FileProtocolEntryKind.File, 10)]
    [TestCase("directory.txt", FileProtocolEntryKind.Directory, 13)]
    [TestCase(".env.local", FileProtocolEntryKind.File, 10)]
    [TestCase("extensionless", FileProtocolEntryKind.File, 13)]
    public void SelectedLength_UsesTheFileStemButKeepsDirectoriesAndDotfiles(string name,
                                                                             FileProtocolEntryKind kind,
                                                                             int expectedLength) {
        Assert.That(FileProtocolRenameCommandLine.SelectedLength(name, kind), Is.EqualTo(expectedLength));
    }

    [Test]
    public void QuoteArgument_MapsTheSelectedStemAcrossQuoteSections() {
        const string name = "report' draft\".tar.gz";
        var selectedLength = FileProtocolRenameCommandLine.SelectedLength(name, FileProtocolEntryKind.File);
        var expectedStem = name[..selectedLength];
        var argument = FileProtocolRenameCommandLine.QuoteArgument(name, selectedLength, out var selectedLengthInInput);
        var selectedArgument = argument[..(selectedLengthInInput + 1)] + argument[^1];

        Assert.That(FileProtocolRenameCommandLine.TryParseArgument(selectedArgument, out var parsedStem), Is.True);
        Assert.That(parsedStem, Is.EqualTo(expectedStem));
    }

    private sealed class Argument {
        [CliArgument]
        [CliRequired]
        public string Name { get; set; }
    }
}
