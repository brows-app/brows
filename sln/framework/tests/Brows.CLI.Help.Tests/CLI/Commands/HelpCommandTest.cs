using Domore.Conf;
using Domore.Conf.Cli;
using NUnit.Framework;
using Brows.Composition;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.CLI.Commands;

[TestFixture]
internal sealed class HelpCommandTest {
    private ICliCommand Subject {
        get => field ??= new HelpCommand { Cli = new(new CliSetup().WithCommandName(_ => "help")) };
        set => field = value;
    }

    [SetUp]
    public void SetUp() {
        Subject = null;
    }

    [Test]
    public void Name_IsHelp() {
        Assert.That(Subject.Name, Is.EqualTo("help"));
    }

    [Test]
    public void Help_IsCorrect() {
        var actual = Subject.Help;
        var expected = @"help [<command>]";
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Manual_IsCorrect() {
        var actual = Subject.Manual;
        var expected = @"
help [<command>]

    Displays general or command-specific help.

    command    The name of a command for command-specific help.";
        Assert.That(actual, Is.EqualTo(expected.Trim()));
    }

    private class JogCommand : CliCommand<JogCommand.Param> {
        protected sealed override Task<object> Run(Param param, ICliCommandContext context, CancellationToken token) {
            throw new NotSupportedException();
        }

        [ConfHelp(@"
            Go for a jog")]
        public class Param {
            [ConfHelp(@"How fast?")]
            [CliRequired]
            [CliArgument]
            public int Speed { get; set; }

            [ConfHelp(@"Set to go barefoot")]
            public bool Barefoot { get; set; }
        }
    }

    private class RunCommand : CliCommand<RunCommand.Param> {
        protected sealed override Task<object> Run(Param param, ICliCommandContext context, CancellationToken token) {
            throw new NotSupportedException();
        }

        [ConfHelp("Go for a run")]
        [CliExample("16 dir=east", "Run at 16mph due East.")]
        public class Param {
            [ConfHelp(@"How fast?")]
            [CliRequired]
            [CliArgument]
            public int Speed { get; set; }

            [ConfHelp(@"What direction?")]
            [CliRequired]
            public Direction Dir { get; set; }

            public enum Direction {
                North, South, East, West
            }
        }
    }

    [Test]
    public async Task Run_OutputsHelp() {
        var actual = default(string);
        await ImportSandbox.Clean(async () => {
            var program = new CliProgram(new() {
                CommandLine = "help",
                ProgramName = "my-prog"
            });
            await program.Task;
            actual = program.Result.ToString();
        });
        var expected = @"
usage: my-prog [command] [arguments] [parameters]
commands:
    help [<command>]
    jog speed<int> [barefoot=<true/false>]
    run speed<int> dir=<north/south/east/west>";
        Assert.That(actual, Is.EqualTo(expected.Trim()));
    }

    [Test]
    public async Task Run_OutputsManual() {
        var actual = default(string);
        await ImportSandbox.Clean(async () => {
            var program = new CliProgram(new() {
                CommandLine = "help run",
                ProgramName = "the-prog"
            });
            await program.Task;
            actual = program.Result.ToString();
        });
        var expected = @"
run speed<int> dir=<north/south/east/west>

    Go for a run

    speed    How fast?

    dir      What direction?

ex. the-prog run 16 dir=east
    Run at 16mph due East.";
        Assert.That(actual, Is.EqualTo(expected.Trim()));
    }
}
